namespace Crm.Application.Common.Consts;

// TODO: Translate logs in different languages.
// TODO: Write unit tests for logging to ensure that all log consts are unique and they are used only once in the application. This will help to avoid duplicate log event.
// TODO: Before end refactor all code values entirely
public static class LogEventIds
{
    // 1000s: Roles, Claims, and Cache
    public const int CacheMiss = 1001;
    public const int AccessRightsUpdated = 1002;
    public const int AccessRightsRemoved = 1003;
    public const int SeedingCache = 1004;
    public const int CacheSeededSuccessfully = 1005;
    public const int CheckingAdminRole = 1006;
    public const int AdminRoleCreatedSuccessfully = 1008;
    public const int AdminRoleAlreadyExists = 1009;
    public const int ClaimsAddedSuccessfully = 1011;
    public const int AllClaimsAlreadyAssigned = 1012;
    public const int AddingClaim = 1013;
    public const int ClaimAddedSuccessfully = 1016;
    public const int CreatingRole = 1017;
    public const int RoleCreationFailed = 1018;
    public const int RoleCreatedSuccessfully = 1019;
    public const int DeletingRole = 1020;
    public const int DeleteRoleCommandHandlerRoleNotFound = 1021;
    public const int CannotModifySystemRole = 1022;
    public const int RoleInUse = 1023;
    public const int RoleDeletedSuccessfully = 1025;
    public const int RemovingClaim = 1026;
    public const int ClaimRemovalFailed = 1028;
    public const int ClaimRemovedSuccessfully = 1029;
    public const int UpdatingRole = 1030;
    public const int UpdateRoleCommandHandlerCannotModifySystemRole = 1032;
    public const int RoleUpdatedSuccessfully = 1034;
    public const int FetchingRoles = 1035;
    public const int RolesFetched = 1036;
    public const int FetchingAvailableAccessRights = 1037;
    public const int FetchingRoleDetails = 1038;
    public const int RoleDetailsFetched = 1040;
    public const int AdminRoleAddingClaimAttempt = 1041;
    public const int AdminRoleRemovingClaimAttempt = 1042;
    public const int InvalidClaimToAddAttempt = 1043;
    public const int InvalidClaimToRemoveAttempt = 1044;

    // 2000s: Users, Authentication, and Profiles
    public const int ActivatingUser = 2001;
    public const int UserAlreadyActive = 2002;
    public const int UserActivatedSuccessfully = 2003;
    public const int AssigningRole = 2004;
    public const int RoleAssignedSuccessfully = 2008;
    public const int AuthenticatingUser = 2009;
    public const int AuthenticationFailed = 2010;
    public const int UserAuthenticatedSuccessfully = 2011;
    public const int CompletingRegistration = 2012;
    public const int UserRegisteredSuccessfully = 2016;
    public const int CreatingUser = 2017;
    public const int UserCreatedSuccessfully = 2019;
    public const int AdminRoleNotFound = 2021;
    public const int DeactivatingUser = 2022;
    public const int UserAlreadyDeactivated = 2023;
    public const int UserDeactivatedSuccessfully = 2024;
    public const int InvitingUser = 2025;
    public const int UserInvitedSuccessfully = 2027;
    public const int InitiatingLogout = 2028;
    public const int LogoutSuccessful = 2031;
    public const int InitiatingTokenRefresh = 2032;
    public const int InvalidAccessTokenProvided = 2033;
    public const int MissingUserIdClaim = 2034;
    public const int UserDeactivated = 2036;
    public const int InvalidOrExpiredRefreshToken = 2037;
    public const int TokenRefreshSuccessful = 2039;
    public const int RemovingRole = 2040;
    public const int RoleRemovedSuccessfully = 2044;
    public const int UpdatingPassword = 2045;
    public const int PasswordUpdatedSuccessfully = 2047;
    public const int UpdatingUserProfile = 2048;
    public const int UserProfileUpdatedSuccessfully = 2049;
    public const int CheckingUsersExistence = 2050;
    public const int UsersExistenceResult = 2051;
    public const int FetchingUserProfile = 2052;
    public const int UserProfileFetched = 2053;
    public const int FetchingUsers = 2054;
    public const int UsersFetched = 2055;
    public const int ValidatingToken = 2056;
    public const int TokenValidatedSuccessfully = 2058;
    public const int CheckingSystemSetupStatus = 2059;
    public const int SystemSetupStatusResult = 2060;
    public const int CannotActivateSelf = 2061;
    public const int CannotDeactivateSelf = 2062;
    public const int CannotDeactivateLastAdmin = 2063;
    public const int FetchingUserPermissions = 2064;
    public const int NoRoleIdsProvidedForPermissions = 2065;
    public const int PermissionsFetchedSuccessfully = 2066;
    public const int AddingUserCustomField = 2067;
    public const int UserCustomFieldAddedSuccessfully = 2068;
    public const int RemovingUserCustomField = 2069;
    public const int UserCustomFieldRemovedSuccessfully = 2070;
    public const int UnauthorizedCustomFieldRemoval = 2071;
    public const int UpdatingUserCustomField = 2072;
    public const int UserCustomFieldUpdatedSuccessfully = 2073;
    public const int UnauthorizedCustomFieldUpdate = 2074;
    public const int DeletingUser = 2075;
    public const int CannotDeleteLastAdmin = 2076;
    public const int UserDeletedSuccessfully = 2077;

    // 3000s: NGOs
    public const int CreatingNgo = 3001;
    public const int NgoCreatedSuccessfully = 3002;
    public const int UpdatingNgo = 3003;
    public const int NgoUpdatedSuccessfully = 3004;

    // 4000s: Transactions and Unit of Work
    public const int BeginningTransaction = 4001;
    public const int TransactionCommitted = 4002;
    public const int TransactionRolledBack = 4003;

    // 5000s: Security, Authorization, and Tokens
    public const int MissingRoleIdClaim = 5001;
    public const int AccessDenied = 5002;
    public const int AccessTokenGenerated = 5003;
    public const int InvitationTokenGenerated = 5004;
    public const int InvalidInvitationToken = 5005;
    public const int TokenValidationFailed = 5006;
    public const int RefreshTokenGenerated = 5007;
    public const int InvalidExpiredToken = 5008;
    public const int ExpiredTokenValidationFailed = 5009;

    // 6000s: Global Errors and Middlewares
    public const int ClientError = 6001;
    public const int ServerError = 6002;

    // 7000s: UI Customizations and Settings
    public const int CreatingLoginPageImage = 7001;
    public const int LoginPageImageCreatedSuccessfully = 7002;
    public const int DeletingLoginPageImage = 7003;
    public const int LoginPageImageDeletedSuccessfully = 7004;
    public const int FetchingAllLoginPageImages = 7005;
    public const int AllLoginPageImagesFetched = 7006;
    public const int FetchingRandomLoginPageImage = 7007;
    public const int RandomLoginPageImageFetched = 7008;
    public const int FetchingLoginPageImageById = 7009;
    public const int LoginPageImageByIdFetchedSuccessfully = 7010;

    // 8000s: Background Services
    public const int CleanupServiceStarting = 8001;
    public const int CleanupServiceStopping = 8002;
    public const int CleanupServiceFatalError = 8003;
    public const int CleanupStarted = 8004;
    public const int CleanupSuccessfullyFinished = 8005;
    public const int CleanupFailed = 8006;
}
