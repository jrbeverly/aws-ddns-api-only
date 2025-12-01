output "table_name" {
  description = "DynamoDB table holding registry entries"
  value       = aws_dynamodb_table.registry.name
}

output "api_endpoint" {
  description = "Base URL of the registry API"
  value       = "https://${aws_api_gateway_rest_api.api.id}.execute-api.${var.aws_region}.amazonaws.com/${var.stage_name}"
}
