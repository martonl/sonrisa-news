Q: Which OpenAI access method should the News Evaluator agent use?
A: Direct OpenAI API (just an API key)

Q: How should the notification service send emails?
A: Stub only (interface + no-op for MVP)

Q: What should the Vue.js admin site include?
A: User list + subscription CRUD + trigger agent manually

Q: Should the plan include Docker / deployment setup?
A: No — local development only for now

Swashbuckle.AspNetCore 10.x pulled in Microsoft.OpenApi 2.0 which reorganized the Models namespace. Also JwtBearer needs an explicit add in .NET 10. I'll remove Swashbuckle, switch to the built-in AddOpenApi, and add JwtBearer explicitly.