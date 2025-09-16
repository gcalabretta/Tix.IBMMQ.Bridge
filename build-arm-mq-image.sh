#!/bin/bash
set -e
repo_dir="mq-container"
if [ -d "$repo_dir" ]; then
  rm -rf "$repo_dir"
fi
git clone https://github.com/ibm-messaging/mq-container.git "$repo_dir"
cd "$repo_dir"
git fetch --all --tags
git checkout tags/9.4.0.0
ARCH=arm64 make build-devserver
