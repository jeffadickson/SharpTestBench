# Use the same C# build implementation from either repository directory.
ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST)))/..)
.PHONY: help build test coverage native native-tools native-runner verify-native run-native-tools run-native-runner
help build test coverage native native-tools native-runner verify-native run-native-tools run-native-runner:
	$(MAKE) -C "$(ROOT)" $@
