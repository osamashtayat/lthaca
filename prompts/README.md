# Prompts

Versioned prompt files, loaded by `IPromptLibrary` (`FilePromptLibrary`). Rules (full version in `docs/ai.md`):

- File name `{name}.v{version}.md`; the id lives in `Ithaca.Application/Ai/IPromptLibrary.cs → Prompts`.
- Never edit a version that has shipped: copy it to `v{n+1}`, switch `Prompts.X` to the new version, run the evals.
- Variables are `{{name}}` and are filled by the caller; the user's own text goes in the request input, never pasted into the prompt.
- Every prompt names its JSON output shape, which matches a C# record in `Ithaca.Application/Ai/`.
