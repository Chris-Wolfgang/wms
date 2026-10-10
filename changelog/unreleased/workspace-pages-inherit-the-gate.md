type: fix

Every console page under a workspace inherits the workspace's gated layout (license and permission check) from the folder's `_Imports.razor`, and an architecture test holds every routable workspace component to it, so a new screen cannot be reachable without the gate by forgetting a `@layout` line.
