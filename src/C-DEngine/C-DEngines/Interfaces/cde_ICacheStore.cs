using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace nsCDEngine.Engines.StorageService
{
    public interface ICacheStore
    {
        public Task<bool> AddOrUpdateRecord<T>(string tableName, T pData) where T : TheDataBase;
        public Task<bool> AddOrUpdateRecords<T>(string tableName, List<T> pData) where T : TheDataBase;
        public Task<T> GetRecordByFilter<T>(string tableName, List<SQLFilter> pFilter) where T : TheDataBase;
        public Task<T> GetRecordByMID<T>(string tableName, Guid mid) where T : TheDataBase;
        public Task<List<T>> GetRecordsByFilter<T>(string tableName, List<SQLFilter> pFilter, Dictionary<string, bool> pSorting,int pageNo, int top) where T : TheDataBase;
        public Task<long> CountRecordsByFilter<T>(string tableName, List<SQLFilter> pFilter) where T : TheDataBase;
        public Task<bool> RemoveRecord<T>(string tableName, T item) where T : TheDataBase;
        public Task<bool> RemoveRecordsByID<T>(string tableName, List<Guid> items) where T : TheDataBase;
        public Task<bool> RemoveRecordByID<T>(string tableName, Guid id) where T : TheDataBase;
        public Task<long> RemoveRecordsByFilter<T>(string tableName, List<SQLFilter> pFilter) where T : TheDataBase;
        public Task RemoveStore<T>(string tableName, bool BackupFirst) where T : TheDataBase;
    }
}
