# Publish a MAJOR update: X.0.0.0 bump (big releases, e.g. 1.0.2.11 -> 2.0.0.0).
# Remember to add the matching "## X.0.0.0" section to changelog.txt first.
# Any extra arguments are passed through to publish.ps1.
& "$PSScriptRoot\publish.ps1" -BumpPart Major @args
exit $LASTEXITCODE
