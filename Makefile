# GNU Make frontend; production orchestration is implemented in C#.
DOTNET ?= dotnet
PYTHON ?= python
CONFIGURATION ?= Release
WORKER_OUTPUT ?= build/workers
ARGS ?= --help
ifeq ($(OS),Windows_NT)
HOST_OS := win
HOST_CPU := $(PROCESSOR_ARCHITECTURE)
EXE := .exe
else
HOST_OS := $(shell uname -s)
HOST_CPU := $(shell uname -m)
EXE :=
endif
ifneq ($(filter Linux,$(HOST_OS)),)
HOST_OS := linux
endif
ifneq ($(filter Darwin,$(HOST_OS)),)
HOST_OS := osx
endif
ifneq ($(filter AMD64 amd64 x86_64,$(HOST_CPU)),)
HOST_ARCH := x64
else ifneq ($(filter ARM64 arm64 aarch64,$(HOST_CPU)),)
HOST_ARCH := arm64
else
HOST_ARCH := unsupported
endif
RID ?= $(HOST_OS)-$(HOST_ARCH)
HELPER := tools/SharpTestBench.Tools/SharpTestBench.Tools.csproj
.PHONY: help build test coverage native native-tools native-runner verify-native run-native-tools run-native-runner
help:
	@echo "build test coverage native native-tools native-runner verify-native run-native-tools run-native-runner"
	@echo "Full helpers: self-contained ReadyToRun. Standalone runner: NativeAOT. RID=$(RID)"
build:
	"$(DOTNET)" build SharpTestBench.slnx -c $(CONFIGURATION) -p:RestoreLockedMode=true --disable-build-servers -m:1
test: build
	"$(DOTNET)" tests/SharpTestBench.Tests/bin/$(CONFIGURATION)/net10.0/SharpTestBench.Tests.dll
	"$(DOTNET)" tests/SharpTestBench.Generators.Tests/bin/$(CONFIGURATION)/net10.0/SharpTestBench.Generators.Tests.dll
	"$(DOTNET)" tests/SharpTestBench.Tools.Tests/bin/$(CONFIGURATION)/net10.0/SharpTestBench.Tools.Tests.dll
	"$(PYTHON)" -m unittest discover -s tests/Tooling -q
coverage: build
	"$(DOTNET)" tool restore
	"$(DOTNET)" run --project "$(HELPER)" -c Release -- code-coverage
native: native-tools native-runner
native-tools:
	"$(DOTNET)" run --project "$(HELPER)" -c Release -- publish-workers --mode tools --rid "$(RID)" --output "$(WORKER_OUTPUT)"
native-runner:
	"$(DOTNET)" run --project "$(HELPER)" -c Release -- publish-workers --mode runner --rid "$(RID)" --output "$(WORKER_OUTPUT)"
verify-native:
	"$(DOTNET)" run --project "$(HELPER)" -c Release -- verify-workers --rid "$(RID)" --output "$(WORKER_OUTPUT)"
run-native-tools:
	"$(WORKER_OUTPUT)/$(RID)/tools/SharpTestBench.Tools$(EXE)" $(ARGS)
run-native-runner:
	"$(WORKER_OUTPUT)/$(RID)/runner/SharpTestBench.Runner$(EXE)" $(ARGS)
