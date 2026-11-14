using Xunit;

// LiteDB использует общий BsonMapper и плохо переносит параллельную
// инициализацию типов из разных тестовых классов.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
