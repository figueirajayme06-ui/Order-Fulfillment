# Unit and Integration Tests

There aren't too many tests which would be regarded as unit tests in the code, mainly I have tried to cover the backend code in integration type tests by creating a LocalDB, applying the some of the database tables, seeding some data for the test then replaying stubbed out real world messages from the IP service bus for various scenarios.

This is completed by using a [database fixture](/src/OF.Tests/Data/DatabaseFixture.cs) to arrange the setup of the db and it's context for running the tests against. Some of the logic in the app needs to test SQL so we couldn't just use the InMemoryDb provider.

## Structure

There is currently on a single test project here which references all off the projects. Almost all of the BOD messages being used for the tests are stored in RESX files.