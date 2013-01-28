﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using SharpRepository.Repository.Queries;
using SharpRepository.Repository.Specifications;

namespace SharpRepository.Repository.Caching
{
    public interface ICompoundKeyCachingStrategy<T, TKey, TKey2>
    {
        bool TryGetResult<TResult>(TKey key, TKey2 key2, Expression<Func<T, TResult>> selector, out TResult result);
        void SaveGetResult<TResult>(TKey key, TKey2 key2, Expression<Func<T, TResult>> selector, TResult result);

        bool TryGetAllResult<TResult>(IQueryOptions<T> queryOptions, Expression<Func<T, TResult>> selector, out IEnumerable<TResult> result);
        void SaveGetAllResult<TResult>(IQueryOptions<T> queryOptions, Expression<Func<T, TResult>> selector, IEnumerable<TResult> result);

        bool TryFindAllResult<TResult>(ISpecification<T> criteria, IQueryOptions<T> queryOptions, Expression<Func<T, TResult>> selector, out IEnumerable<TResult> result);
        void SaveFindAllResult<TResult>(ISpecification<T> criteria, IQueryOptions<T> queryOptions, Expression<Func<T, TResult>> selector, IEnumerable<TResult> result);

        bool TryFindResult<TResult>(ISpecification<T> criteria, IQueryOptions<T> queryOptions, Expression<Func<T, TResult>> selector, out TResult result);
        void SaveFindResult<TResult>(ISpecification<T> criteria, IQueryOptions<T> queryOptions, Expression<Func<T, TResult>> selector, TResult result);

        void Add(TKey key, TKey2 key2, T result);
        void Update(TKey key, TKey2 key2, T result);
        void Delete(TKey key, TKey2 key2, T result);
        void Save();

        ICachingProvider CachingProvider { get; set; }
    }
}