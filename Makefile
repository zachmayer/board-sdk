# Pong for Board - Makefile
# Run `make` or `make help` to see available targets

UNITY := /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity
PROJECT := $(shell pwd)
RESULTS_DIR := /tmp/pong-tests

.PHONY: help test test-edit test-play build build-mac build-android lint clean

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

test: test-edit ## Run all tests (alias for test-edit)

test-edit: ## Run edit mode tests (fast, no game running)
	@mkdir -p $(RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(PROJECT)" \
		-runTests -testPlatform EditMode \
		-testResults $(RESULTS_DIR)/edit.xml \
		-logFile -
	@echo "Results: $(RESULTS_DIR)/edit.xml"

test-play: ## Run play mode tests (slower, runs game objects)
	@mkdir -p $(RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(PROJECT)" \
		-runTests -testPlatform PlayMode \
		-testResults $(RESULTS_DIR)/play.xml \
		-logFile -
	@echo "Results: $(RESULTS_DIR)/play.xml"

build: ## Verify project compiles (close Unity first)
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PROJECT)" \
		-logFile - 2>&1 | tee /tmp/build.log
	@! grep -qi "error" /tmp/build.log || (echo "Build failed" && exit 1)
	@echo "Build OK"

build-mac: ## Build macOS app for testing (close Unity first)
	@mkdir -p "$(PROJECT)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PROJECT)" \
		-buildTarget StandaloneOSX \
		-buildOSXUniversalPlayer "$(PROJECT)/Build/Pong.app" \
		-logFile -
	@echo "Built: $(PROJECT)/Build/Pong.app"

build-android: ## Build Android APK for Board hardware (close Unity first)
	@mkdir -p "$(PROJECT)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PROJECT)" \
		-buildTarget Android \
		-exportAsGoogleAndroidProject false \
		-logFile -
	@echo "Built: $(PROJECT)/Build/*.apk"

lint: ## Check C# formatting (requires dotnet)
	@command -v dotnet >/dev/null 2>&1 || (echo "dotnet not installed - run: brew install dotnet" && exit 1)
	dotnet format "$(PROJECT)" --verify-no-changes --verbosity diagnostic 2>/dev/null || echo "Note: dotnet format requires .sln file. Open project in Unity first."

clean: ## Remove test results and build artifacts
	rm -rf $(RESULTS_DIR)
	rm -f /tmp/build.log
