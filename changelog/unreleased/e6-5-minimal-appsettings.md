type: feature

`appsettings` is limited to bootstrap keys (database, key ring path, URLs, log level, bootstrap admin); every other key in an appsettings JSON file, and every other `Wms:` key from any provider (an environment variable, the command line), is ignored and named with its source in one startup warning.
