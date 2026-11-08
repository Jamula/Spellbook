# AI model and parallel work guide

This guide sets project practices for AI-assisted Spellbook work. Platform model names, subscriptions, limits, and reset times can change, so check the account's current usage page rather than treating this file as a quota reference.

## Choose a model for the task

Use the least costly model that can complete the task reliably, based on the model choices currently available in the work environment.

| Work | Model choice |
| --- | --- |
| Small documentation edits, formatting, routine issue updates, and straightforward code changes | A fast, lower-cost model |
| Feature implementation, multi-file debugging, and focused code review | A general-purpose coding model |
| Security-sensitive design, difficult failures, or a review with substantial architectural consequences | A stronger reasoning model; keep the task narrow and ask for evidence |

For a code change, give the model the relevant files, constraints, and a concrete done condition. Ask it to identify assumptions and run only the checks needed for that change. Keep one integrator responsible for reconciling edits and reviewing the final diff.

## Parallel work

- Cap a Spellbook task at **two active agents**, including the integrator when counting agents.
- Delegate only work that has separate files or a clear research boundary. Assign each agent a deliverable, a stop condition, and a checkpoint before broad implementation.
- Batch independent tool calls and keep assignments bounded. Avoid asking multiple agents to solve the same issue or repeatedly re-read the full project.
- Stop parallel work when changes begin to overlap or the integrator can finish faster than coordination would take.

This cap is a project practice, not a platform limit. It follows the recorded project experience in issue #52: one parallel agent reached a usage limit during a multi-agent Phase 1 task. That notice gave a reset time without a timezone. The project did not have access to authoritative account-level usage settings in that review, so no account quota or reset schedule is claimed here.

## Usage limits and recovery

Before planning a long or parallel task, check the AI platform's **Settings → Usage** page for current remaining usage and reset information. Follow any provider-specific controls shown there. Do not infer a subscription tier, quota, or reset time from a previous notice.

If an agent reaches a limit:

1. Stop new delegations and save the current diff, notes, and completed findings.
2. Check the account usage page for the provider's authoritative reset or recovery options.
3. Let the integrator continue independent work if possible; otherwise resume after the provider makes capacity available.
4. Reassign only the unfinished bounded portion. Do not repeat completed work or use paid APIs/services to bypass a limit.

## Privacy and cost

Do not put credentials, private prompts, user deck data, or personal photos/videos in usage notes. Do not enable paid services or paid APIs for AI work. Capture only the operational signal needed to explain a project decision, such as that a quota was reached and the usage page was unavailable.
