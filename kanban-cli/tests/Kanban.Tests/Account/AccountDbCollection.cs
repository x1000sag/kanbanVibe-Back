using Xunit;

namespace Kanban.Tests.Account;

[CollectionDefinition("AccountDb", DisableParallelization = true)]
public sealed class AccountDbCollection : ICollectionFixture<AccountApiFactory>;