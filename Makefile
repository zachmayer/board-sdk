# Board Games - Makefile
# Run `make` or `make help` to see available targets

UNITY := /Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity
ROOT := $(shell pwd)
BDB := $(ROOT)/bin/bdb
ADB := /Applications/Unity/Hub/Editor/6000.3.8f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
BOARD_IP := 192.168.1.203

# Pong game
PONG := $(ROOT)/games/pong
PONG_APK := $(PONG)/Build/Pong.apk
PONG_PACKAGE := fun.board.pong
RESULTS_DIR := /tmp/pong-tests

# Golf Wall game
GW := $(ROOT)/games/golf-wall
GW_APK := $(GW)/Build/GolfWall.apk
GW_PACKAGE := fun.board.golfwall
GW_RESULTS_DIR := /tmp/golfwall-tests

.PHONY: help test test-edit test-play build build-mac build-android setup-scene deploy logs stop bdb-status bdb-fix clean
.PHONY: gw-test gw-test-edit gw-test-play gw-setup-scene gw-build-android gw-deploy gw-deploy-wifi gw-logs gw-clean
.PHONY: adb-connect adb-status

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

test: test-edit ## Run all tests (alias for test-edit)

test-edit: ## Run edit mode tests (fast, no game running)
	@mkdir -p $(RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(PONG)" \
		-runTests -testPlatform EditMode \
		-testResults $(RESULTS_DIR)/edit.xml \
		-logFile -
	@echo "Results: $(RESULTS_DIR)/edit.xml"

test-play: ## Run play mode tests (slower, runs game objects)
	@mkdir -p $(RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(PONG)" \
		-runTests -testPlatform PlayMode \
		-testResults $(RESULTS_DIR)/play.xml \
		-logFile -
	@echo "Results: $(RESULTS_DIR)/play.xml"

build: ## Verify project compiles (close Unity first)
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PONG)" \
		-logFile - 2>&1 | tee /tmp/build.log
	@! grep -qi "error" /tmp/build.log || (echo "Build failed" && exit 1)
	@echo "Build OK"

build-mac: ## Build macOS app for testing (close Unity first)
	@mkdir -p "$(PONG)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PONG)" \
		-buildTarget StandaloneOSX \
		-buildOSXUniversalPlayer "$(PONG)/Build/Pong.app" \
		-logFile -
	@echo "Built: $(PONG)/Build/Pong.app"

setup-scene: ## Setup Pong scene (creates game objects, saves scene)
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PONG)" \
		-executeMethod Pong.Editor.BatchSetup.SetupAndSave \
		-logFile -
	@echo "Scene setup complete"

build-android: setup-scene ## Build Android APK for Board hardware (close Unity first)
	@mkdir -p "$(PONG)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PONG)" \
		-buildTarget Android \
		-executeMethod Pong.Editor.BuildScript.Build \
		-logFile -
	@echo "Built: $(PONG_APK)"

deploy: $(PONG_APK) ## Install and launch Pong on Board
	$(BDB) install $(PONG_APK)
	$(BDB) launch $(PONG_PACKAGE)

logs: ## Stream logs from Board (Ctrl+C to stop)
	$(BDB) logs $(PONG_PACKAGE)

stop: ## Stop app on Board
	$(BDB) stop $(PONG_PACKAGE)

bdb-status: ## Check Board connection
	$(BDB) status

bdb-fix: ## Fix bdb macOS permissions
	xattr -cr $(BDB)
	codesign --force --deep --sign - $(BDB)

clean: ## Remove test results and build artifacts
	rm -rf $(RESULTS_DIR) "$(PONG)/Build"
	rm -f /tmp/build.log

# ============================================================
# Golf Wall targets
# ============================================================

gw-test: gw-test-play ## Run Golf Wall tests (alias for gw-test-play)

gw-test-play: ## Run Golf Wall tests (all tests run in play mode)
	@mkdir -p $(GW_RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(GW)" \
		-runTests -testPlatform PlayMode \
		-testResults $(GW_RESULTS_DIR)/play.xml \
		-logFile -
	@echo "Results: $(GW_RESULTS_DIR)/play.xml"

gw-setup-scene: ## Setup Golf Wall scene
	$(UNITY) -batchmode -nographics -quit -projectPath "$(GW)" \
		-executeMethod GolfWall.Editor.BatchSetup.SetupAndSave \
		-logFile -
	@echo "Golf Wall scene setup complete"

gw-build-android: gw-setup-scene ## Build Golf Wall APK for Board
	@mkdir -p "$(GW)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(GW)" \
		-buildTarget Android \
		-executeMethod GolfWall.Editor.BuildScript.Build \
		-logFile -
	@echo "Built: $(GW_APK)"

gw-deploy: $(GW_APK) ## Install and launch Golf Wall on Board (USB)
	$(BDB) install $(GW_APK)
	$(BDB) launch $(GW_PACKAGE)

gw-deploy-wifi: $(GW_APK) ## Install and launch Golf Wall over WiFi (no cable)
	$(ADB) install -r $(GW_APK)
	$(ADB) shell am start -n $(GW_PACKAGE)/com.unity3d.player.UnityPlayerActivity

gw-logs: ## Stream Golf Wall logs from Board
	$(BDB) logs $(GW_PACKAGE)

gw-clean: ## Remove Golf Wall build artifacts
	rm -rf $(GW_RESULTS_DIR) "$(GW)/Build"

# ============================================================
# ADB / WiFi debugging targets
# ============================================================

adb-connect: ## Connect to Board over WiFi (run once after Board reboot)
	$(ADB) connect $(BOARD_IP):5555

adb-status: ## Check ADB connections (USB and WiFi)
	$(ADB) devices
