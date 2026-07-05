# Board Games - Makefile
# Run `make` or `make help` to see available targets

UNITY := /Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity
ROOT := $(shell pwd)
BDB := $(ROOT)/bin/bdb
BC := $(HOME)/.local/bin/board-connect
SCRATCH := $(HOME)/claude/scratch

# Pong game
PONG := $(ROOT)/games/pong
PONG_APK := $(PONG)/Build/Pong.apk
PONG_APP := $(PONG)/Build/Pong.app
PONG_PACKAGE := fun.board.pong
RESULTS_DIR := $(SCRATCH)/pong-tests

# Golf Wall game
GW := $(ROOT)/games/golf-wall
GW_APK := $(GW)/Build/GolfWall.apk
GW_APP := $(GW)/Build/GolfWall.app
GW_PACKAGE := fun.board.golfwall
GW_RESULTS_DIR := $(SCRATCH)/golfwall-tests

.PHONY: help test test-edit test-play build build-mac sim build-android setup-scene deploy deploy-wifi logs logs-wifi stop bdb-status bdb-fix clean
.PHONY: gw-test gw-test-play gw-setup-scene gw-build-android gw-build-mac gw-sim gw-deploy gw-deploy-wifi gw-logs gw-logs-wifi gw-clean
.PHONY: connect-install connect-ls connect-pair screenshot

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-16s\033[0m %s\n", $$1, $$2}'

test: test-play ## Run all Pong tests (alias for test-play; the test assembly is play-mode)

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
	@mkdir -p $(SCRATCH)
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PONG)" \
		-logFile - 2>&1 | tee $(SCRATCH)/build.log
	@! grep -qi "error" $(SCRATCH)/build.log || (echo "Build failed" && exit 1)
	@echo "Build OK"

build-mac: setup-scene ## Build Pong macOS app for local mouse testing (close Unity first)
	@mkdir -p "$(PONG)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PONG)" \
		-buildTarget StandaloneOSX \
		-executeMethod Pong.Editor.BuildScript.BuildMac \
		-logFile -
	@echo "Built: $(PONG_APP)"

sim: build-mac ## Build and launch Pong on this Mac (mouse controls paddles)
	open "$(PONG_APP)"

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

deploy: $(PONG_APK) ## Install and launch Pong on Board (USB, legacy bdb)
	$(BDB) install $(PONG_APK)
	$(BDB) launch $(PONG_PACKAGE)

deploy-wifi: $(PONG_APK) ## Install and launch Pong on Board over WiFi (board-connect)
	$(BC) install $(PONG_APK) --launch

logs: ## Stream Pong logs from Board (USB, Ctrl+C to stop)
	$(BDB) logs $(PONG_PACKAGE)

logs-wifi: ## Stream Pong logs from Board over WiFi (Ctrl+C to stop)
	$(BC) logs $(PONG_PACKAGE) --follow

stop: ## Stop Pong on Board (USB)
	$(BDB) stop $(PONG_PACKAGE)

bdb-status: ## Check Board USB connection (legacy bdb)
	$(BDB) status

bdb-fix: ## Fix bdb macOS permissions
	xattr -cr $(BDB)
	codesign --force --deep --sign - $(BDB)

clean: ## Remove test results and build artifacts
	rm -rf $(RESULTS_DIR) "$(PONG)/Build"
	rm -f $(SCRATCH)/build.log

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

gw-build-mac: gw-setup-scene ## Build Golf Wall macOS app for local mouse testing
	@mkdir -p "$(GW)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(GW)" \
		-buildTarget StandaloneOSX \
		-executeMethod GolfWall.Editor.BuildScript.BuildMac \
		-logFile -
	@echo "Built: $(GW_APP)"

gw-sim: gw-build-mac ## Build and launch Golf Wall on this Mac (click-drag to swing)
	open "$(GW_APP)"

gw-deploy: $(GW_APK) ## Install and launch Golf Wall on Board (USB, legacy bdb)
	$(BDB) install $(GW_APK)
	$(BDB) launch $(GW_PACKAGE)

gw-deploy-wifi: $(GW_APK) ## Install and launch Golf Wall over WiFi (board-connect)
	$(BC) install $(GW_APK) --launch

gw-logs: ## Stream Golf Wall logs from Board (USB)
	$(BDB) logs $(GW_PACKAGE)

gw-logs-wifi: ## Stream Golf Wall logs over WiFi (Ctrl+C to stop)
	$(BC) logs $(GW_PACKAGE) --follow

gw-clean: ## Remove Golf Wall build artifacts
	rm -rf $(GW_RESULTS_DIR) "$(GW)/Build"

# ============================================================
# Board Connect (WiFi deploy) targets
# ============================================================

connect-install: ## Install/update the board-connect CLI (official installer)
	curl -fsSL https://dev.board.fun/connect/install | sh
	$(BC) --version

connect-ls: ## Discover Boards on the local network
	$(BC) ls

connect-pair: ## One-time pairing: make connect-pair HOST=<addr> (tap Approve on Board)
	@test -n "$(HOST)" || (echo "Usage: make connect-pair HOST=<board-address>  (discover with 'make connect-ls')" && exit 1)
	$(BC) pair $(HOST)
	$(BC) use $(HOST)

screenshot: ## Capture a screenshot of the Board's screen over WiFi
	@mkdir -p $(SCRATCH)
	$(BC) screenshot --out $(SCRATCH)/board-shot.png
	@echo "Saved: $(SCRATCH)/board-shot.png"
