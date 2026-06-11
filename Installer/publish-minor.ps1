# Publish a MINOR update: 1.X.0.0 bump (new features, e.g. 1.0.2.11 -> 1.1.0.0).
# Remember to add the matching "## X.Y.0.0" section to changelog.txt first.
# Any extra arguments are passed through to publish.ps1.
& "$PSScriptRoot\publish.ps1" -BumpPart Minor @args
exit $LASTEXITCODE
