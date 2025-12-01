variable "aws_region" {
  description = "AWS region to deploy into"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Name prefix applied to all resources"
  type        = string
  default     = "aws-ddns-api-only"
}

variable "lambda_package" {
  description = "Path to the packaged Lambda zip"
  type        = string
  default     = "lambda.zip"
}

variable "stage_name" {
  description = "API Gateway stage name"
  type        = string
  default     = "prod"
}
