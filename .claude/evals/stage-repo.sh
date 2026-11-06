#!/bin/sh
# Stages the repo into an eval run's empty workspace (the current directory).
# Each case's scaffold.sh calls this, because claude plugin eval only runs a
# scaffold script that sits inside the case folder.
#
# PIN is a tag on the commit the cases were written against. Graders assume the
# code as it stands there (file paths, the existing health and start-up code), so
# moving it means re-checking every case. A tag, not a hash: squash merges drop
# branch commits from main, and the tag keeps the commit reachable. See README.md.
set -eu

PIN=evals-baseline-1

# MSBuild otherwise leaves build-server processes running after the scaffold. They
# keep this run's HOME and temp folders, so later runs' restores connect to them and
# fail once the eval tool seals or deletes those folders.
export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0

repo_dir="$(cd "$(dirname "$0")" && git rev-parse --show-toplevel)"
git -C "$repo_dir" archive "$PIN" | tar -x -C .

# The "with skills" arm gets the skills from the plugin under test; leaving a copy
# in the workspace would give them to the "without" arm too. settings.json enables
# marketplace plugins the eval sandbox doesn't have.
rm -rf .claude/skills .claude/settings.json

dotnet tool restore
dotnet restore
