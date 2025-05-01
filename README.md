Employee Review Application – README 

Author: Chad McGrath (chadmcgrath@gmail.com) 

Overview 

This application is designed for managing employee performance reviews. It supports role-based access for Admins, Employees, and Reviewers, with authentication handled via JWT tokens. 

Important! When Swagger loads, you'll notice three buttons at the top of the page that allow you to switch between roles for testing purposes. This makes it easier to verify how different users interact with the api. 

The reviewers Id is 2. 

The employees id is: 1 
 
 

This implementation is a bit quick and dirty due to time constraints, it’s far from perfect, but it should pass muster! 

Design Decisions & Assumptions 

Transparency & Access 

Transparency is a key principle. Under Anaplan's I ACT REAL values, employees can see all their reviews, and reviewers can see any reviews they've conducted, as well as their own employee reviews. The reviewer ID is a foreign key to the employee table in the database. 

Architecture & Technologies 

This is not  a proof of concept (PoC) or minimum viable product (MVP), some areas are over-engineered to demonstrate architectural principles. 

Implements Domain-Driven Design (DDD) with domain entities and services. I think it’s good idea to come up with a team policy about when bypassing a service is appropriate or not. 

 

The database schema is generated from domain entities using the Fluent API, instead of defining Entity Framework (EF) entities directly.  

CQRS is not used, as it would be overkill for this type of application. 

Some DTO conversions happen directly in repositories, optimizing queries for better performance. 

The application uses Unit of Work, even though there's no batching or other functionality typically associated with the pattern, primarily to demonstrate the pattern. I think it’s good idea to come up with a team policy about when injecting a UnitOfWork pattern is sometimes bypassed in lieu of a single repository or not.  

Security & Secrets Management 

For ease of testing, secrets (such as API keys) are hardcoded in this candidate application. 

Normally, sensitive information would be stored in Azure Key Vault, but in this demo, secrets are embedded for convenience. 

Controllers & Pagination 

Some controllers support pagination, primarily to demonstrate how it's implemented. 

Many files are bloated and should ideally be refactored into separate, more concise files. (Program.cs and Security.cs are too big, and interfaces should be separate). 

Soft Deletes & Repository Implementation 

The Employee repository implements deletion, but it throws a "Not Allowed" exception instead of fully omitting the method. This makes it clear that the application follows a Soft Delete approach. 

The implementation does not violate the Open/Closed Principle or Liskov Substitution Principle, because the inherited delete method still serves a purpose. 

Authorization & Authentication 

Uses policy-based authorization for employees, reviewers, and admins. 

Employees can view all their reviews. 

Reviewers can view any reviews they have conducted. 

Authentication is handled via JWT tokens. 

Testing Approach 

Both unit tests and integration tests exist. 

More integration tests for authorization could be added, but they require additional effort. 

For ease of testing, the application creates and seeds an SQLite database in the project. 

Remember the buttons at the top of Swagger! 

Primary To Do: 

More unit and integration tests, especially for authorization. Implementing authorization tetss using JWT for different roles and claims could be tricky. 

Separate files for many classes and separate files for interfaces. 

Some functionality is only implemented in a development environment, and would have to be adjusted for other environments (such as assigning swagger JWT tokens). 

I hope I wasn’t remiss in using SQLite.   

 

 
