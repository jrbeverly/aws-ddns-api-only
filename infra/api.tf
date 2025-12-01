# Registry endpoints: GET /identity/{identity} reads one entry,
# POST /identity/{identity} records an address. DescribeTable is needed
# because Table.LoadTable calls it at cold start.

data "aws_iam_policy_document" "lambda_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "lambda_exec" {
  name               = "${var.project_name}-lambda-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
}

resource "aws_iam_policy" "lambda_dynamo" {
  name = "${var.project_name}-dynamodb"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["dynamodb:DescribeTable", "dynamodb:GetItem", "dynamodb:PutItem"]
      Resource = aws_dynamodb_table.registry.arn
    }]
  })
}

resource "aws_iam_role_policy_attachment" "dynamo_attach" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = aws_iam_policy.lambda_dynamo.arn
}

resource "aws_iam_role_policy_attachment" "logs_attach" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_cloudwatch_log_group" "registry_lookup" {
  name              = "/aws/lambda/${aws_lambda_function.registry_lookup.function_name}"
  retention_in_days = 14
}

resource "aws_lambda_function" "registry_lookup" {
  function_name = "${var.project_name}-lookup"
  handler       = "AddressRegistry::AddressRegistry.Function::FunctionHandler"
  runtime       = "dotnet8"
  role          = aws_iam_role.lambda_exec.arn
  filename      = var.lambda_package
  # Without this, a rebuilt lambda.zip is never redeployed: apply reports
  # no changes and the API keeps serving the previous package.
  source_code_hash = filebase64sha256(var.lambda_package)
  memory_size      = 128
  timeout          = 10

  environment {
    variables = {
      DDB_TABLE = aws_dynamodb_table.registry.name
    }
  }
}

resource "aws_api_gateway_rest_api" "api" {
  name = var.project_name
}

# catch-all under the root
resource "aws_api_gateway_resource" "proxy" {
  rest_api_id = aws_api_gateway_rest_api.api.id
  parent_id   = aws_api_gateway_rest_api.api.root_resource_id
  path_part   = "{proxy+}"
}

# ANY method on the proxy
resource "aws_api_gateway_method" "all_methods" {
  rest_api_id   = aws_api_gateway_rest_api.api.id
  resource_id   = aws_api_gateway_resource.proxy.id
  http_method   = "ANY"
  authorization = "NONE"
  request_parameters = {
    "method.request.path.proxy" = true
  }
}

resource "aws_api_gateway_integration" "all_integrations" {
  rest_api_id             = aws_api_gateway_rest_api.api.id
  resource_id             = aws_api_gateway_resource.proxy.id
  http_method             = aws_api_gateway_method.all_methods.http_method
  type                    = "AWS_PROXY"
  integration_http_method = "POST"
  uri                     = aws_lambda_function.registry_lookup.invoke_arn
  request_parameters = {
    "integration.request.path.proxy" = "method.request.path.proxy"
  }
}

resource "aws_api_gateway_deployment" "deployment" {
  rest_api_id = aws_api_gateway_rest_api.api.id
  stage_name  = var.stage_name

  # Republish when the route changes; without triggers an edited method or
  # integration is never deployed to the stage.
  triggers = {
    redeployment = sha1(jsonencode([
      aws_api_gateway_resource.proxy.id,
      aws_api_gateway_method.all_methods.id,
      aws_api_gateway_integration.all_integrations.id
    ]))
  }

  depends_on = [aws_api_gateway_integration.all_integrations]
}

resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.registry_lookup.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.api.execution_arn}/*/*"
}
