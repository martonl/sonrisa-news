# Agents Summary

## Solution Purpose
This solutions is for subscribers can subsribe to news notifications. They can set up alerts and notified them when something important happens in the world — like breaking news, market movements, natural disasters, that kind of thing. Solution concentrates only on MVPs.

It provides:
- Subscriber management through Admin site
- Scheduled AI agent what get the news feed, evaluates the fresh news and send notifications to subscribers
- Notification service what get the notifications through queue and send it through Email and Slack
- SQLite persistence via Entity Framework Core

## Main Capabilities

### Authentication & Accounts
- Register and log in users (use standard Asp.Net Core Identity)
- Users can create subscriptions for Email and Slack
- Use JWT token authentication
- Self-service endpoints for current user profile and subsciption actions

### Evaluating news
- Use an AI agent for getting RSS feed from biggest news feed
- The fresh news send through queue to notification service
- Store last run, and evaluate only the fresh news

### Notification features
- Receive news from queue
- Get subscriptions and send news notifications (both email and slack) to them 

## Technical Notes
- Use modular monolith architecture as MVP based on Asp.Net Core
- Target framework: .NET 10 (`net10.0`)
- Asp.Net Core API project: `NewsApp`
- Admin site: VueJs project use JWT tokens
- Data store: SQLite (`news.db`)
- Auth: Asp.Net Core Identity
- Queue: Use .Net 10 channel feature as queue
- News evaluator AI agent: Open AI through Microsoft Agent Framework https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp
- Create Integration tests for testing scheduled AI agent
- Create API endpoint tests for testing User login, register, subscription CRUD

## Overall Interpretation
A JWT-secured backend with basic user administration and scheduled News evaluating, sending notifications to subscribers.