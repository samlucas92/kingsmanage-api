# Meta integration deployment

The Meta publishing feature is dormant until the deployment has these environment variables:

- `META_APP_ID`: Meta app id.
- `META_APP_SECRET`: Meta app secret.
- `META_REDIRECT_URI`: Exact frontend callback URL, for example `https://app.example.com/organization/integrations/meta/callback`.
- `META_TOKEN_ENCRYPTION_KEY`: Base64-encoded 32-byte key used only to encrypt Meta access tokens at rest.
- `META_GRAPH_API_VERSION`: Optional; defaults to `v24.0`.

Generate the encryption key with a cryptographically secure secret generator and store it in the deployment's secret manager. Keep the same value between deployments. Rotating it requires reconnecting every Meta integration currently stored with the old key.

## Meta app configuration

Add the exact redirect URL to **Facebook Login for Business → Valid OAuth Redirect URIs**. Request these permissions in the Meta app:

- `pages_show_list`
- `pages_manage_posts`
- `pages_read_engagement`
- `instagram_basic`
- `instagram_content_publish`

Facebook Pages must be managed by the connecting account. Instagram destinations must be professional accounts connected to those Pages. The app can be tested with Meta app roles while in development mode; publishing for customer organisations requires the relevant Advanced Access and App Review approval.

The API never receives a Facebook password. User and Page access tokens are encrypted before MongoDB persistence and are never returned by API view models.

## Worker and storage

The API hosts the publishing worker. `MetaIntegration:PublishingEnabled` and `MetaIntegration:PollIntervalSeconds` control it. Scheduled times are persisted in UTC.

Studio artwork is converted to JPEG, stored through the existing managed-file/R2 path and exposed to Meta with a two-hour signed read URL during publishing. The bucket does not need to be public.
