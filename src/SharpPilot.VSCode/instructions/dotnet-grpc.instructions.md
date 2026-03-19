---
description: "Use when defining gRPC services, proto files, client/server implementation, or streaming patterns in .NET."
applyTo: "**/*.{cs,fs,vb,proto}"
---
# gRPC Guidelines

## Proto Design

- **Do** treat proto field numbers as immutable — never reuse or reassign a field number after it has been published; use `reserved` to retire old numbers.
- **Do** use `string` for identifiers, `google.protobuf.Timestamp` for dates, and wrapper types (`google.protobuf.StringValue`) when you need to distinguish null from default.
- **Do** version proto packages (e.g., `package myservice.v1;`) — breaking changes go in a new version.
- **Don't** use enums with a default value of `0` that carries business meaning — reserve `0` for `UNSPECIFIED` so missing values are detectable.

## Service Implementation

- **Do** set deadlines on every RPC call from the client — calls without deadlines can hang indefinitely.
- **Do** propagate `CancellationToken` through the call chain — respect `context.CancellationToken` in server handlers.
- **Do** return appropriate gRPC status codes — never return `OK` with an error payload; use `NotFound`, `InvalidArgument`, `PermissionDenied`, etc.
- **Do** use server-streaming for large result sets and client-streaming for uploads — avoid loading entire datasets into a single response message.
- **Don't** throw raw exceptions from gRPC handlers — catch and convert to `RpcException` with a meaningful `StatusCode` and detail message.
- **Don't** use gRPC for browser clients without a proxy — browsers cannot make HTTP/2 gRPC calls directly; use gRPC-Web or an Envoy/YARP proxy.

## Performance

- **Do** reuse `GrpcChannel` instances — creating a channel per call wastes TCP connections and TLS handshakes.
- **Don't** mix JSON and Protobuf serialization in the same service boundary — use the generated Protobuf message types end-to-end.
