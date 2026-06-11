# Re-publish the CURRENT version - no version bump.
# Use after a failed/interrupted publish run, or to rebuild the same version
# locally. Note: if the GitHub release for this version already exists,
# the release step will fail on purpose (delete the release first with
# "gh release delete vX.Y.Z.W" if you really mean to replace it).
# Any extra arguments are passed through to publish.ps1.
& "$PSScriptRoot\publish.ps1" -SkipBump @args
exit $LASTEXITCODE
