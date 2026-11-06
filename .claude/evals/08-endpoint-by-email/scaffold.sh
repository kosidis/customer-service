#!/bin/sh
# claude plugin eval only runs scaffold scripts inside the case folder; the real work is shared.
exec sh "$(dirname "$0")/../stage-repo.sh"
