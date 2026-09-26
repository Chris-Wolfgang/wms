type: feature

Console sessions: absolute lifetime (`auth.session.lifetime`, default 8 h, at most 24 h) and idle timeout (`auth.session.idle_timeout`, default 30 min, sliding on any request) are settings; a password change, a disable, or a role change ends the user's existing sessions immediately.
