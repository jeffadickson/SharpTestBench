# Original development history

`pre-github.bundle` preserves the complete local development history through the
GitHub bootstrap preparation commit. The GitHub branch begins with an API import
because direct Git transport was not authenticated in the publishing environment.

To inspect the original commits independently:

```sh
git clone history/pre-github.bundle original-history
```

The bundle is a fixed archive, not a copy to update for each future commit.
Future development belongs to the GitHub branch history.
