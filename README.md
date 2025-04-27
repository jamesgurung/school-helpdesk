# School Helpdesk

School Helpdesk is a free, open-source web application that that makes it easy for parents and carers to submit tickets for streamlined communication with school staff.

> :warning: This project is in early development and it is not yet ready for production use.

### Setup

1. Create a Postmark server.

2. Create an OpenAI account and generate an API key.

3. Create a general purpose v2 storage account in Microsoft Azure, and within it create:
    * Tables: `tickets` and `comments`
    * Blob containers: `config` and `attachments`

4. Within the `config` blob container, upload a blank file `keys.xml`. Generate a SAS URL for this file with read/write permissions and a distant expiry. This will be used to store the application's data protection keys so that auth cookies persist across app restarts.

5. Within the `config` blob container, create a file `students.csv` with the following headers and populate it with all students in your school. Where a student has more than one parent, repeat the student details across multiple rows. To correctly represent accented characters in student names, save the file as 'CSV UTF-8'.

    ```csv
    FirstName,LastName,TutorGroup,Relationship,ParentTitle,ParentFirstName,ParentLastName,ParentEmailAddress
    ```

6. Within the `config` blob container, create a file `staff.csv` with the following headers and populate it with all staff in your school who should have access to the helpdesk.
    ```csv
    Email,Title,FirstName,LastName
    ```

7. Create an Azure app registration.
    * Name - `School Helpdesk`
    * Redirect URI - `https://<app-website-domain>/signin-oidc`
    * Implicit grant - ID tokens
    * Supported account types - Accounts in this organizational directory only
    * API permissions - `Microsoft Graph - User.Read`
    * Token configuration - add an optional claim of type ID: `upn`

8. Create an Azure App Service web app.
    * Publish mode - Container
    * Operating system - Linux
    * Image source - Other container registries
    * Docker Hub access type - Public
    * Image and tag - `jamesgurung/school-helpdesk:latest`
    * Startup command: (blank)

9. Configure the following environment variables for the web app:

    * `Azure__ClientId` - the client ID of your Azure app registration
    * `Azure__DataProtectionBlobUri` - the SAS URL for the keys file you created earlier
    * `Azure__StorageAccountName` - the name of your Azure Storage account
    * `Azure__StorageAccountKey` - the key for your Azure Storage account
    * `Azure__TenantId` - your Azure tenant ID
    * `OpenAIApiKey` - the API key for your OpenAI account
    * `Postmark__InboundAuthKey` - a secret UUID of your choice, used to verify that incoming emails are from Postmark
    * `Postmark__ServerToken` - the token for your Postmark server
    * `School__AdminUsers__0` - the email address of the first admin user (subsequent admins can be configured by adding items with incrementing indices)
    * `School__AppWebsite` - the host name where this app will be hosted, e.g. `example.com`
    * `School__Name` - the name of your school

### Contributing

If you have a question or feature request, please open an issue.

To contribute improvements to this project, or to adapt the code for the specific needs of your school, you are welcome to fork the repository.

Pull requests are welcome; please open an issue first to discuss.