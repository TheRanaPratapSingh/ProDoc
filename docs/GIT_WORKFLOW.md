# ProDoc Git workflow

## Branches

| Branch | Purpose |
| --- | --- |
| `main` | Default integration branch; keep buildable |
| `develop` | Optional shared development branch |
| `feature/<name>` | New work |
| `bugfix/<name>` | Fixes |
| `hotfix/<name>` | Urgent production fixes |

Do not force-push shared branches (`main` / `develop`).

## Daily commands

```bash
git status
git pull
git switch -c feature/my-change
git add -A
git commit -m "Explain the reason for the change."
git push -u origin HEAD
```

## From Cursor

After finishing a feature:

1. Review `git status` and the diff.
2. Confirm no `.env`, keys, or local DB passwords are staged.
3. Ask the agent to commit (or commit yourself) with a clear message.
4. `git push` to the GitHub remote.
5. Open a pull request when collaborating.

## Recommended GitHub settings

- Protect `main` (no force push; require PR where practical).
- Enable Dependabot / vulnerability alerts.
- Enable secret scanning if available on the plan.
- Keep CI required for merges once the workflow is stable.

Do not change repository visibility without owner approval.
