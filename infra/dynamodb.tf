# Registry entries: stable identity -> most recently accepted network address.
#
# Access patterns, both point operations on the partition key:
#   report - PutItem by identity (overwrites the previous entry)
#   lookup - GetItem by identity
# Nothing scans; no secondary index is needed.
#
# Entry shape (exactly four attributes; only `identity` is a key attribute):
#   identity      (S) stable system identity, chosen by the registering client
#   address       (S) most recently accepted network address
#   last_reported (S) ISO-8601 UTC timestamp of the most recent report,
#                    updated whether or not the address changed
#   last_changed  (S) ISO-8601 UTC timestamp of the most recent report that
#                    changed the address value

resource "aws_dynamodb_table" "registry" {
  name         = "${var.project_name}-registry"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "identity"

  attribute {
    name = "identity"
    type = "S"
  }
}
