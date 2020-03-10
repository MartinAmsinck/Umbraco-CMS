﻿using System;
using System.Web.Script.Serialization;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using System.Linq;
using Newtonsoft.Json;
using umbraco.cms.businesslogic.web;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Sync;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// A cache refresher used for non-published content, this is primarily to notify Examine indexes to update and to refresh the RuntimeCacheRefresher
    /// </summary>
    public sealed class UnpublishedPageCacheRefresher : TypedCacheRefresherBase<UnpublishedPageCacheRefresher, IContent>, IJsonCacheRefresher
    {
        protected override UnpublishedPageCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return new Guid(DistributedCache.UnpublishedPageCacheRefresherId); }
        }

        public override string Name
        {
            get { return "Unpublished Page Refresher"; }
        }

        #region Static helpers

        /// <summary>
        /// Converts the json to a JsonPayload object
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        internal static JsonPayload[] DeserializeFromJsonPayload(string json)
        {
            var jsonObject = JsonConvert.DeserializeObject<JsonPayload[]>(json);
            return jsonObject;
        }


        internal static string SerializeToJsonPayloadForPermanentDeletion(params int[] contentIds)
        {
            var items = contentIds.Select(x => new JsonPayload
            {
                Id = x,
                Operation = OperationType.Deleted
            }).ToArray();
            var json = JsonConvert.SerializeObject(items);
            return json;
        }
        internal static string SerializeToJsonPayload(OperationType operationType, params IContent[] contents)
        {
            var items = contents.Select(x => new JsonPayload
            {
                Id = x.Id,
                Key = x.Key,
                Operation = operationType
            }).ToArray();
            var json = JsonConvert.SerializeObject(items);
            return json;
        }

        #endregion

        #region Sub classes

        internal enum OperationType
        {
            Deleted,
            Refresh
        }

        internal class JsonPayload
        {
            public int Id { get; set; }
            public Guid? Key { get; set; }
            public OperationType Operation { get; set; }
        }

        #endregion

        public override void RefreshAll()
        {
            ApplicationContext.Current.Services.IdkMap.ClearCache();
            ClearAllIsolatedCacheByEntityType<IContent>();
            ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();
            DistributedCache.Instance.ClearDomainCacheOnCurrentServer();
            content.Instance.ClearPreviewXmlContent();
            base.RefreshAll();
        }

        public override void Refresh(int id)
        {
            ClearRepositoryCacheItemById(id);
            ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();
            content.Instance.UpdateSortOrder(id);
            var d = new Document(id);
            content.Instance.UpdateDocumentCache(d);
            content.Instance.UpdatePreviewXmlContent(d);
            DistributedCache.Instance.ClearDomainCacheOnCurrentServer();
            base.Refresh(id);
        }

        public override void Remove(int id)
        {
            ApplicationContext.Current.Services.IdkMap.ClearCache(id);
            ClearRepositoryCacheItemById(id);
            ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();
            DistributedCache.Instance.ClearDomainCacheOnCurrentServer();
            content.Instance.ClearPreviewXmlContent(id);
            base.Remove(id);
        }


        public override void Refresh(IContent instance)
        {
            ClearRepositoryCacheItemById(instance.Id);
            ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();
            content.Instance.UpdateSortOrder(instance);
            var d = new Document(instance);
            content.Instance.UpdateDocumentCache(d);
            content.Instance.UpdatePreviewXmlContent(d);
            DistributedCache.Instance.ClearDomainCacheOnCurrentServer();
            base.Refresh(instance);
        }

        public override void Remove(IContent instance)
        {
            ApplicationContext.Current.Services.IdkMap.ClearCache(instance.Id);
            ClearRepositoryCacheItemById(instance.Id);
            ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();
            DistributedCache.Instance.ClearDomainCacheOnCurrentServer();
            content.Instance.ClearPreviewXmlContent(instance.Id);
            base.Remove(instance);
        }

        /// <summary>
        /// Implement the IJsonCacheRefresher so that we can bulk delete the cache based on multiple IDs for when the recycle bin is emptied
        /// </summary>
        /// <param name="jsonPayload"></param>
        public void Refresh(string jsonPayload)
        {
            ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();

            foreach (var payload in DeserializeFromJsonPayload(jsonPayload))
            {
                ClearRepositoryCacheItemById(payload.Id);
                ClearRepositoryCacheItemById(payload.Key);
                ClearAllIsolatedCacheByEntityType<PublicAccessEntry>();

                if (payload.Operation == OperationType.Deleted)
                {
                    ApplicationContext.Current.Services.IdkMap.ClearCache(payload.Id);
                    content.Instance.ClearPreviewXmlContent(payload.Id);
                    base.Remove(payload.Id);
                }

                if (payload.Operation == OperationType.Refresh)
                {
                    content.Instance.UpdateSortOrder(payload.Id);
                    var d = new Document(payload.Id);
                    content.Instance.UpdateDocumentCache(d);
                    content.Instance.UpdatePreviewXmlContent(d);

                    base.Refresh(payload.Id);
                }
            }

            DistributedCache.Instance.ClearDomainCacheOnCurrentServer();

            OnCacheUpdated(Instance, new CacheRefresherEventArgs(jsonPayload, MessageType.RefreshByJson));
        }

        private void ClearRepositoryCacheItemById(int id)
        {
            var contentCache = ApplicationContext.Current.ApplicationCache.IsolatedRuntimeCache.GetCache<IContent>();
            if (contentCache)
            {
                contentCache.Result.ClearCacheItem(RepositoryBase.GetCacheIdKey<IContent>(id));
            }
        }

        private void ClearRepositoryCacheItemById(Guid? key)
        {
            var contentCache = ApplicationContext.Current.ApplicationCache.IsolatedRuntimeCache.GetCache<IContent>();
            if (contentCache)
            {
                contentCache.Result.ClearCacheItem(RepositoryBase.GetCacheIdKey<IContent>(key));
            }
        }
    }
}