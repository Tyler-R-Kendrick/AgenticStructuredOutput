# GitHub Workflows

## Optimize Agent Prompt

The `optimize-prompt.yml` workflow automatically runs the AgenticStructuredOutput.Optimization CLI to improve the agent prompt using evaluation test cases.

### Features

- **Manual Trigger**: Run on-demand via GitHub Actions UI
- **Scheduled Run**: Automatically runs weekly (Sundays at 2 AM UTC)
- **Configurable**: Customize max iterations and test case path
- **Artifact Storage**: Saves optimized prompt and detailed reports

### Triggering the Workflow

#### Manual Trigger (Recommended)

1. Go to **Actions** tab in GitHub
2. Select **Optimize Agent Prompt** workflow
3. Click **Run workflow**
4. Configure options:
   - **Max iterations**: Number of optimization iterations (default: 5)
   - **Test cases path**: Custom JSONL file path (default: test-cases-eval.jsonl)
5. Click **Run workflow** button

#### Scheduled Trigger

The workflow runs automatically every Sunday at 2 AM UTC. You can modify the schedule in the workflow file:

```yaml
schedule:
  - cron: '0 2 * * 0'  # Weekly on Sundays at 2 AM UTC
```

### Configuration Options

**Input Parameters:**

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `max_iterations` | Maximum optimization iterations | `5` | No |
| `test_cases_path` | Path to test cases JSONL file | `test-cases-eval.jsonl` | No |

**Environment Variables:**

- `GITHUB_TOKEN`: Automatically provided by GitHub Actions (used for GitHub Models API access)

### Artifacts

The workflow generates the following artifacts (retained for 90 days):

| File | Description |
|------|-------------|
| `optimized-prompt.md` | The optimized agent prompt |
| `baseline-prompt.md` | Original prompt for comparison |
| `optimization-output.log` | Complete CLI output with metrics |
| `diff-report.md` | Markdown diff between baseline and optimized |
| `schema.json` | Schema used for optimization |
| `test-cases-eval.jsonl` | Test cases used for evaluation |

### Workflow Steps

1. **Checkout repository**: Gets latest code
2. **Setup .NET 9**: Installs .NET SDK
3. **Restore & Build**: Compiles the solution
4. **Run optimization CLI**: Executes optimization with configured parameters
5. **Copy artifacts**: Collects optimized prompt and related files
6. **Generate diff report**: Creates comparison between baseline and optimized
7. **Upload artifacts**: Stores results for download
8. **Create summary**: Displays results in GitHub Actions summary

### Understanding the Output

#### Success Indicators

- ✅ **Exit code 0**: Optimization improved the prompt
- ⚠️ **Exit code 1**: Optimization completed but no improvement found

#### Artifacts Breakdown

**optimized-prompt.md:**
- The new, optimized agent prompt
- May include additional examples, constraints, or refinements
- Ready to be reviewed and committed

**diff-report.md:**
- Shows exact changes made to the prompt
- Includes statistics (length, lines changed)
- Helps understand what optimizations were applied

**optimization-output.log:**
- Complete CLI output with detailed metrics
- Shows baseline vs. optimized scores
- Lists iteration history and strategy applications
- Useful for debugging and analysis

### Applying the Optimized Prompt

#### Option 1: Manual Review and Commit

1. Download artifacts from workflow run
2. Review `diff-report.md` and `optimized-prompt.md`
3. If satisfied, replace `agent-instructions.md`:
   ```bash
   cp optimized-prompt.md agent-instructions.md
   git add agent-instructions.md
   git commit -m "Apply optimized prompt from workflow run #123"
   git push
   ```

#### Option 2: Create a Pull Request

```bash
# Download artifacts
gh run download <run-id> -n optimized-prompt-<run-number>

# Create branch
git checkout -b optimize-prompt-run-<run-number>

# Apply optimized prompt
cp artifacts/optimized-prompt.md agent-instructions.md

# Commit and push
git add agent-instructions.md
git commit -m "Apply optimized prompt from workflow run <run-number>

- Improved from baseline score X.XX to Y.YY
- Applied Z iterations of optimization
- See workflow run for detailed metrics"

git push origin optimize-prompt-run-<run-number>

# Create PR via GitHub UI or CLI
gh pr create --title "Apply optimized prompt from run <run-number>" \
  --body "See workflow run <run-number> for optimization details"
```

### Monitoring and Troubleshooting

#### Check Workflow Status

- View in Actions tab: `Actions > Optimize Agent Prompt > Latest run`
- See summary with optimization results
- Download artifacts for detailed analysis

#### Common Issues

**Issue: Workflow fails with authentication error**
- Cause: `GITHUB_TOKEN` doesn't have access to GitHub Models
- Solution: Ensure repository has access to GitHub Models API

**Issue: No improvement shown (exit code 1)**
- Cause: Baseline prompt is already optimal for test cases
- Solution: Review test cases or try different optimization strategies

**Issue: Optimization takes too long**
- Cause: Too many test cases or high iteration count
- Solution: Reduce `max_iterations` or use subset of test cases

**Issue: Out of memory**
- Cause: Large prompts or many parallel evaluations
- Solution: Reduce parallel tasks in CLI configuration

### Cost Considerations

**GitHub Actions:**
- Public repos: Free
- Private repos: Consumes minutes from plan

**LLM API Calls (GitHub Models):**
- ~200-280 API calls per optimization run
- Cost: ~$0.20-0.40 per run (gpt-4o-mini)
- Scheduled weekly: ~$1-2/month

### Customization

#### Change Schedule

Edit `.github/workflows/optimize-prompt.yml`:

```yaml
schedule:
  # Daily at midnight UTC
  - cron: '0 0 * * *'
  
  # Monthly on 1st at 3 AM UTC
  - cron: '0 3 1 * *'
  
  # Weekdays at 9 AM UTC
  - cron: '0 9 * * 1-5'
```

#### Add Notification

Add a notification step:

```yaml
- name: Notify on Slack
  if: success()
  uses: slackapi/slack-github-action@v1
  with:
    webhook-url: ${{ secrets.SLACK_WEBHOOK_URL }}
    payload: |
      {
        "text": "Prompt optimization completed: Run #${{ github.run_number }}"
      }
```

#### Auto-commit Improvements

**⚠️ Not recommended** - but possible:

```yaml
- name: Auto-commit if improved
  if: steps.optimize.outputs.exit_code == '0'
  run: |
    git config user.name "GitHub Actions Bot"
    git config user.email "actions@github.com"
    git checkout -b auto-optimize-${{ github.run_number }}
    git add agent-instructions.md
    git commit -m "Auto-optimize prompt (run ${{ github.run_number }})"
    git push origin auto-optimize-${{ github.run_number }}
```

### Best Practices

1. **Review Before Applying**: Always review optimization output before committing
2. **Test Optimized Prompts**: Validate in staging before production
3. **Monitor Metrics**: Track optimization improvements over time
4. **Adjust Test Cases**: Update test-cases-eval.jsonl as requirements change
5. **Version Control**: Keep history of optimization decisions
6. **Set Reasonable Limits**: Don't over-optimize with too many iterations

### Security Considerations

- `GITHUB_TOKEN` is automatically provided and scoped to repository
- Artifacts are private to repository (unless repo is public)
- LLM API calls use GitHub Models with repository token
- No secrets are exposed in workflow logs

### Examples

#### Quick Optimization (3 iterations)

```bash
# Via GitHub CLI
gh workflow run optimize-prompt.yml -f max_iterations=3
```

#### Custom Test Cases

```bash
gh workflow run optimize-prompt.yml \
  -f max_iterations=10 \
  -f test_cases_path=custom-test-cases.jsonl
```

#### Check Latest Run

```bash
gh run list --workflow=optimize-prompt.yml --limit 1
gh run view <run-id> --log
```

### Integration with CI/CD

You can trigger this workflow from other workflows:

```yaml
- name: Trigger optimization
  uses: actions/github-script@v7
  with:
    script: |
      await github.rest.actions.createWorkflowDispatch({
        owner: context.repo.owner,
        repo: context.repo.repo,
        workflow_id: 'optimize-prompt.yml',
        ref: 'main',
        inputs: {
          max_iterations: '10',
          test_cases_path: 'integration-test-cases.jsonl'
        }
      });
```

## Support

For issues or questions:
1. Check workflow logs in Actions tab
2. Review artifacts (especially `optimization-output.log`)
3. Consult AgenticStructuredOutput.Optimization documentation
4. Open an issue with workflow run number and logs
