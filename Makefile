LAMBDA_PROJ  := AddressRegistry/src/AddressRegistry/AddressRegistry.csproj
LAMBDA_TESTS := AddressRegistry/test/AddressRegistry.Tests/AddressRegistry.Tests.csproj
CLIENT_TESTS := ReportingClient/test/ReportingClient.Tests/ReportingClient.Tests.csproj

export DOTNET_NOLOGO := 1
export DOTNET_CLI_TELEMETRY_OPTOUT := 1

.PHONY: build test package deploy e2e destroy

build:
	dotnet build $(LAMBDA_TESTS)
	dotnet build $(CLIENT_TESTS)

test: build
	dotnet test $(LAMBDA_TESTS) --no-build
	dotnet test $(CLIENT_TESTS) --no-build

package:
	rm -rf .build infra/lambda.zip
	dotnet publish $(LAMBDA_PROJ) -c Release -o .build/lambda
	cd .build/lambda && zip -qr ../../infra/lambda.zip .

deploy: package
	terraform -chdir=infra init -input=false
	terraform -chdir=infra apply -auto-approve -input=false

e2e: build
	./e2e.sh

destroy:
	terraform -chdir=infra destroy -auto-approve -input=false
