## Manually Updating sttp.net Libraries

When [net-cppapi](https://github.com/sttp/net-cppapi) libraries need to be manually updated, copy the following files:

| From | To |
| ---- | -- |
| sttp/net-cppapi/build/output/x64/Release/lib/netstandard2.0/sttp.net.dll | sttp/connection-tester/Assets/sttp.net |
| sttp/net-cppapi/build/output/x64/Release/lib/sttp.net.lib.dll | sttp/connection-tester/Assets/sttp.net/Plugins/x86_64 |
| sttp/net-cppapi/build/output/x64/Release/lib/sttp.net.lib.so | sttp/connection-tester/Assets/sttp.net/Plugins/x86_64 |
| sttp/net-cppapi/build/output/x86/Release/lib/sttp.net.lib.dll | sttp/connection-tester/Assets/sttp.net/Plugins/x86 |
