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
- `pages_read_user_content`
- `read_insights`
- `instagram_basic`
- `instagram_content_publish`
- `instagram_manage_insights`
- `business_management`

Facebook Pages must be managed by the connecting account. Instagram destinations must be professional accounts connected to those Pages. The app can be tested with Meta app roles while in development mode; publishing and insights for customer organisations require the relevant Advanced Access and App Review approval.

Yepset discovers Pages both from the connecting user's direct Page list and from Business Portfolios to which the user has access. Business Portfolio discovery checks both owned Pages and partner/client Pages. Add `business_management` and `pages_read_user_content` to the Meta app before deploying this version, then disconnect and reconnect existing integrations so their user tokens include the new permissions. Direct Page discovery remains available when `business_management` is declined or the user does not belong to a Business Portfolio.

Yepset reads Page and Instagram post metadata plus post-level insights. Overview responses are cached for five minutes. Some metrics may be missing for new posts, unsupported post types or accounts that do not meet Meta's eligibility thresholds.

The API never receives a Facebook password. User and Page access tokens are encrypted before MongoDB persistence and are never returned by API view models.

## Worker and storage

The API hosts a short-lived delivery worker. `MetaIntegration:PublishingEnabled` and `MetaIntegration:PollIntervalSeconds` control it. The Studio does not expose future Yepset-side scheduling: content is either saved in Yepset, queued for immediate publishing, or sent to Facebook as an unpublished draft. Instagram does not provide an equivalent persistent draft endpoint, so Instagram content remains in Yepset when Facebook draft is selected. Legacy scheduled records are still processed at their persisted UTC time.

Studio artwork is converted to JPEG, stored through the existing managed-file/R2 path and exposed to Meta with a two-hour signed read URL during publishing. The bucket does not need to be public.
