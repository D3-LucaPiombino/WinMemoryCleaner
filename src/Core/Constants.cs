namespace WinMemoryCleaner.Core
{
    public static class Constants
    {
        public static class Windows
        {
            public const string DebugPrivilege = "SeDebugPrivilege";
            public const string IncreaseQuotaName = "SeIncreaseQuotaPrivilege";
            public const int MemoryFlushModifiedList = 3;
            public const int MemoryPurgeLowPriorityStandbyList = 5;
            public const int MemoryPurgeStandbyList = 4;
            public const int PrivilegeEnabled = 2;
            public const string ProfileSingleProcessName = "SeProfileSingleProcessPrivilege";
            public const int SystemCombinePhysicalMemoryInformation = 130;
            public const int SystemFileCacheInformation = 21;
            public const int SystemMemoryListInformation = 80;
        }
    }
}
