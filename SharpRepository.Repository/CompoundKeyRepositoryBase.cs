using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using SharpRepository.Repository.Caching;
using SharpRepository.Repository.Helpers;
using SharpRepository.Repository.Queries;
using SharpRepository.Repository.Specifications;
using SharpRepository.Repository.Transactions;

namespace SharpRepository.Repository
{
    public abstract partial class CompoundKeyRepositoryBase<T, TKey, TKey2> : ICompoundKeyRepository<T, TKey, TKey2> where T : class
    {
        // the caching strategy used
        private ICompoundKeyCachingStrategy<T, TKey, TKey2> _cachingStrategy;

        // the query manager uses the caching strategy to determine if it should check the cache or run the query
        private CompoundKeyQueryManager<T, TKey, TKey2> _queryManager;

        // just the type name, used to find the primary key if it is [TypeName]Id
        private readonly string _typeName;
        protected string TypeName
        {
            get { return _typeName; }
        }
        
        public bool CacheUsed
        {
            get { return _queryManager.CacheUsed; }
        }

        public IBatch<T> BeginBatch()
        {
            // Return the privately scoped batch via the publicly available interface. 
            // This ensures that a repository alone can initiate a new batch.
            return new Batch(this);
        }
      
        private bool BatchMode { get; set; }

        protected CompoundKeyRepositoryBase(ICompoundKeyCachingStrategy<T, TKey, TKey2> cachingStrategy = null)
        {
            if (typeof(T) == typeof(TKey))
            {
                // this check is mainly because of the overloaded Delete methods Delete(T) and Delete(TKey), ambiguous reference if the generics are the same
                throw new InvalidOperationException("The repository type and the primary key type can not be the same.");
            }

            CachingStrategy = cachingStrategy ?? new NoCachingStrategy<T, TKey, TKey2>();
            _typeName = typeof (T).Name;
        }

        public ICompoundKeyCachingStrategy<T, TKey, TKey2> CachingStrategy 
        {
            get { return _cachingStrategy; } 
            set
            {
                _cachingStrategy = value ?? new NoCachingStrategy<T, TKey, TKey2>();
                _queryManager = new CompoundKeyQueryManager<T, TKey, TKey2>(_cachingStrategy);
            }
        } 

        public abstract IQueryable<T> AsQueryable();

        // These are the actual implementation that the derived class needs to implement
        protected abstract IQueryable<T> GetAllQuery();
        protected abstract IQueryable<T> GetAllQuery(IQueryOptions<T> queryOptions);

        public IEnumerable<T> GetAll()
        {
            return GetAll(null);
        }

        public IEnumerable<T> GetAll(IQueryOptions<T> queryOptions)
        {
            return _queryManager.ExecuteGetAll(
                () => GetAllQuery(queryOptions).ToList(),
                null,
                queryOptions
                );
        }

        public IEnumerable<TResult> GetAll<TResult>(Expression<Func<T, TResult>> selector, IQueryOptions<T> queryOptions = null)
        {
            if (selector == null) throw new ArgumentNullException("selector");

            return _queryManager.ExecuteGetAll(
                () =>  GetAllQuery(queryOptions).Select(selector).ToList(),
                selector,
                queryOptions
                );
        }

        // These are the actual implementation that the derived class needs to implement
        protected abstract T GetQuery(TKey key, TKey2 key2);

        public abstract IRepositoryQueryable<TResult> Join<TJoinKey, TInner, TResult>(IRepositoryQueryable<TInner> innerRepository, Expression<Func<T, TJoinKey>> outerKeySelector, Expression<Func<TInner, TJoinKey>> innerKeySelector, Expression<Func<T, TInner, TResult>> resultSelector)
            where TInner : class
            where TResult : class;

        public T Get(TKey key, TKey2 key2)
        {
            return _queryManager.ExecuteGet(
                () => GetQuery(key, key2),
                null,
                key,
                key2
                );
        }

        public TResult Get<TResult>(TKey key, TKey2 key2, Expression<Func<T, TResult>> selector)
        {
            if (selector == null) throw new ArgumentNullException("selector");

            return _queryManager.ExecuteGet(
                () =>
                {
                    var result = GetQuery(key, key2);
                    if (result == null)
                        return default(TResult);

                    var results = new[] { result };
                    return results.AsQueryable().Select(selector).First();
                },
                selector,
                key,
                key2
                );
        }

        // These are the actual implementation that the derived class needs to implement
        protected abstract IQueryable<T> FindAllQuery(ISpecification<T> criteria);
        protected abstract IQueryable<T> FindAllQuery(ISpecification<T> criteria, IQueryOptions<T> queryOptions);

        public IEnumerable<T> FindAll(ISpecification<T> criteria, IQueryOptions<T> queryOptions = null)
        {
            if (criteria == null) throw new ArgumentNullException("criteria");

            return _queryManager.ExecuteFindAll(
                () => FindAllQuery(criteria, queryOptions).ToList(),
                criteria,
                null,
                queryOptions
                );
        }

        public IEnumerable<TResult> FindAll<TResult>(ISpecification<T> criteria, Expression<Func<T, TResult>> selector, IQueryOptions<T> queryOptions = null)
        {
            if (criteria == null) throw new ArgumentNullException("criteria");

            return _queryManager.ExecuteFindAll(
                () => FindAllQuery(criteria, queryOptions).Select(selector).ToList(),
                criteria,
                selector,
                queryOptions
                );
        }

        public IEnumerable<T> FindAll(Expression<Func<T, bool>> predicate, IQueryOptions<T> queryOptions = null)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");

            return FindAll(new Specification<T>(predicate), queryOptions);
        }

        public IEnumerable<TResult> FindAll<TResult>(Expression<Func<T, bool>> predicate, Expression<Func<T, TResult>> selector, IQueryOptions<T> queryOptions = null)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (selector == null) throw new ArgumentNullException("selector");

            return FindAll(new Specification<T>(predicate), selector, queryOptions);
        }

        // These are the actual implementation that the derived class needs to implement
        protected abstract T FindQuery(ISpecification<T> criteria);
        protected abstract T FindQuery(ISpecification<T> criteria, IQueryOptions<T> queryOptions);

        public T Find(ISpecification<T> criteria, IQueryOptions<T> queryOptions = null)
        {
            if (criteria == null) throw new ArgumentNullException("criteria");

            return _queryManager.ExecuteFind(
                () => FindQuery(criteria, queryOptions),
                criteria,
                null,
                null
                );
        }

        public TResult Find<TResult>(ISpecification<T> criteria, Expression<Func<T, TResult>> selector, IQueryOptions<T> queryOptions = null)
        {
            if (criteria == null) throw new ArgumentNullException("criteria");
            if (selector == null) throw new ArgumentNullException("selector");

            return _queryManager.ExecuteFind(
                () =>
                    {
                        var result = FindQuery(criteria, queryOptions);
                        if (result == null)
                            return default(TResult);

                        var results = new[] { result };
                        return results.AsQueryable().Select(selector).First();
                    },
                criteria,
                selector,
                null
                );
        }

        public T Find(Expression<Func<T, bool>> predicate, IQueryOptions<T> queryOptions = null)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");

            return Find(new Specification<T>(predicate), queryOptions);
        }

        public TResult Find<TResult>(Expression<Func<T, bool>> predicate, Expression<Func<T, TResult>> selector, IQueryOptions<T> queryOptions = null)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (selector == null) throw new ArgumentNullException("selector");

            return Find(new Specification<T>(predicate), selector, queryOptions);
        }

        // This is the actual implementation that the derived class needs to implement
        protected abstract void AddItem(T entity);

        public void Add(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");

            ProcessAdd(entity, BatchMode);
        }

        // used from the Add method above and the Save below for the batch save
        private void ProcessAdd(T entity, bool batchMode)
        {
            AddItem(entity);
            if (batchMode) return;

            Save();

            TKey key;
            TKey2 key2;
            if (GetPrimaryKey(entity, out key, out key2))
                _queryManager.OnItemAdded(key, key2, entity);
        }

        public void Add(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException("entities");

            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        // This is the actual implementation that the derived class needs to implement
        protected abstract void DeleteItem(T entity);

        public void Delete(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");

            ProcessDelete(entity, BatchMode);
        }

        // used from the Delete method above and the Save below for the batch save
        private void ProcessDelete(T entity, bool batchMode)
        {
            DeleteItem(entity);
            if (batchMode) return;

            Save();

            TKey key;
            TKey2 key2;
            if (GetPrimaryKey(entity, out key, out key2))
                _queryManager.OnItemDeleted(key, key2, entity);
        }

        public void Delete(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                Delete(entity);
            }
        }

        public void Delete(TKey key, TKey2 key2)
        {
            var entity = Get(key, key2);

            if (entity == null) throw new ArgumentException("No entity exists with this key.", "key");

            Delete(entity);
        }

        // This is the actual implementation that the derived class needs to implement
        protected abstract void UpdateItem(T entity);

        public void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");

            ProcessUpdate(entity, BatchMode);
        }

        // used from the Update method above and the Save below for the batch save
        private void ProcessUpdate(T entity, bool batchMode)
        {
            UpdateItem(entity);
            if (batchMode) return;

            Save();

            TKey key;
            TKey2 key2;
            if (GetPrimaryKey(entity, out key, out key2))
                _queryManager.OnItemUpdated(key, key2, entity);
        }

        public void Update(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException("entities");

            foreach (var entity in entities)
            {
                Update(entity);
            }
        }

        protected abstract void SaveChanges();

        private void Save()
        {
            SaveChanges();
            
            _queryManager.OnSaveExecuted(); 
        }

        
        public abstract void Dispose();

        protected virtual bool GetPrimaryKey(T entity, out TKey key, out TKey2 key2) 
        {
            key = default(TKey);
            key2 = default(TKey2);

            var propInfo = GetPrimaryKeyPropertyInfo();

            // if there is no property that matches then return false
            if (propInfo == null || propInfo.Length != 2)
                return false;

            if (propInfo[0].GetValue(entity, null) is TKey && propInfo[1].GetValue(entity, null) is TKey2)
            {
                key = (TKey) propInfo[0].GetValue(entity, null);
                key2 = (TKey2) propInfo[1].GetValue(entity, null);
            }
            else
            {
                key2 = (TKey2)propInfo[0].GetValue(entity, null);
                key = (TKey)propInfo[1].GetValue(entity, null);
            }
           
           return true;
        }

        protected virtual bool SetPrimaryKey(T entity, TKey key, TKey2 key2)
        {
            var propInfo = GetPrimaryKeyPropertyInfo();

            // if there is no property that matches then return false
            if (propInfo == null || propInfo.Length != 2)
                return false;

            if (propInfo[0].GetValue(entity, null) is TKey && propInfo[1].GetValue(entity, null) is TKey2)
            {
                propInfo[0].SetValue(entity, key, null);
                propInfo[1].SetValue(entity, key2, null);
            }
            else
            {
                propInfo[0].SetValue(entity, key2, null);
                propInfo[1].SetValue(entity, key, null);
            }

            return true;
        }

        protected virtual ISpecification<T> ByPrimaryKeySpecification(TKey key, TKey2 key2)
        {
            var propInfo = GetPrimaryKeyPropertyInfo();
            if (propInfo == null || propInfo.Length != 2)
                return null;

            Expression<Func<T, bool>> lambda, lambda2;

            if (propInfo[0].PropertyType == typeof(TKey) && propInfo[1].PropertyType == typeof(TKey2))
            {
                lambda = Linq.DynamicExpression.ParseLambda<T, bool>(String.Format("{0} == {1}", propInfo[0].Name, key));
                lambda2 = Linq.DynamicExpression.ParseLambda<T, bool>(String.Format("{0} == {1}", propInfo[1].Name, key2));
            }
            else
            {
                lambda = Linq.DynamicExpression.ParseLambda<T, bool>(String.Format("{0} == {1}", propInfo[1].Name, key));
                lambda2 = Linq.DynamicExpression.ParseLambda<T, bool>(String.Format("{0} == {1}", propInfo[0].Name, key2));
            }

            return new Specification<T>(lambda).And(lambda2);
        }

        protected PropertyInfo[] GetPrimaryKeyPropertyInfo()
        {
            // checks for properties in this order that match TKey type
            //  1) RepositoryPrimaryKeyAttribute
            //  2) Id
            //  3) [Type Name]Id
            var type = typeof(T);
            var keyType = typeof(TKey);
            var keyType2 = typeof (TKey2);

            return type.GetProperties().Where(x => x.HasAttribute<RepositoryPrimaryKeyAttribute>() && (x.PropertyType == keyType || x.PropertyType == keyType2)).ToArray();
        }

//        private static PropertyInfo GetPropertyCaseInsensitive(IReflect type, string propertyName, Type propertyType)
//        {
//            // make the property reflection lookup case insensitive
//            const BindingFlags bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;
//
//            return type.GetProperty(propertyName, bindingFlags, null, propertyType, new Type[0], new ParameterModifier[0]);
//        }

        public abstract IEnumerator<T> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}