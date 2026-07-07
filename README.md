# sonrisa-news

## Scope of this project
- create an MVP app for demonstrate my AI coding skills
- for reaching the MVP I use a modular-monolith architecture
- the solution must be work, it must be demoable state (add correct OpenAI API key into appsettings.json and everything works)

## Not scope of this project
- not create a fully polished, state-of-the-art production ready code

## Going forward to production
- Polish the code
- Add proper logging
- Add health check
- Use production ready databases instead of SQLite
- Extract modules into microservices (it means split current DB into authdb, subscriptiondb etc.)
- Test against performance
- fine-grain AI based news filtering and prioritizing
- create CICD pipeline, use AI where possible (in PRs, test run, test coverage etc.)