# sql-join-analyzer
Windows App to help break down complex SQL Server join statements to find which join clauses are over/under filtering.

Allows integrated authentication to a sql server to run a query against and breaks down all join clauses to show how many rows would be returned for each clause.

Helps identify wrong joins that over or under filter the data.

Extremely useful for complex joins such as star schemas.
