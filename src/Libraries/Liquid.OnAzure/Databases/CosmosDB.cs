﻿using Liquid.Base;
using Liquid.Domain;
using Liquid.Interfaces;
using Liquid.Repository;
using Liquid.Runtime;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Liquid.OnAzure
{
    /// <summary>
    /// Provides a unique namespace to store and access your Azure Storage data objects.
    /// </summary>
    /// <remarks>
    /// Creates a CosmosDB instance from specific settings to a given name provided on appsettings.
    /// The configuration should be provided like "CosmosDB_{sufixName}". 
    /// </remarks>
    /// <param name="suffixName">Name of configuration</param>
    public partial class CosmosDB(string suffixName) : LightRepository
    {
        private const int TIMEOUT_IN_SECONDS = 45;

        private CosmosClient _client;
        private CosmosDBConfiguration _config;

        private CosmosClientOptions _clientOptions;
        private Database _db;
        private string _databaseId;
        private string _containerPrefix;
        private readonly string _suffixName = suffixName;
        private string _endpoint;
        private string _authKey;
        private ILightMediaStorage _mediaStorage;

        /// <inheritdoc/>
        public override ILightMediaStorage MediaStorage { get => _mediaStorage; }

        private static string isReseeding = "false";
        private static bool IsReseeding
        {
            get
            {
                return isReseeding == "true";
            }
            set
            {
                lock (isReseeding)
                {
                    isReseeding = value ? "true" : "false";
                }
            }
        }

        /// <summary>
        /// Creates a CosmosDB instance from default settings provided on appsettings.
        /// </summary>
        public CosmosDB() : this("") { }

        /// <inheritdoc/>
        public override void Initialize()
        {
            if (string.IsNullOrWhiteSpace(this._suffixName)) // Load specific settings if provided
                _config = LightConfigurator.LoadConfig<CosmosDBConfiguration>($"{nameof(CosmosDB)}");
            else
                _config = LightConfigurator.LoadConfig<CosmosDBConfiguration>($"{nameof(CosmosDB)}_{this._suffixName}");

            //Initialize CosmosDB instance with provided settings            
            LoadConfigurations();
        }

        private void LoadConfigurations()
        {
            Setup = true;
            _endpoint = _config.Endpoint;
            _authKey = _config.AuthKey;

            _clientOptions = new()
            {
                Serializer = new CosmosSystemTextJsonSerializer(),
                RequestTimeout = TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS)
            };

            if (!string.IsNullOrWhiteSpace(_config.ConnectionMode) && !string.IsNullOrWhiteSpace(_config.ConnectionProtocol))
            {
                _clientOptions.ConnectionMode = (ConnectionMode)Enum.Parse(typeof(ConnectionMode), _config.ConnectionMode, true);

                if (WorkBench.IsDevelopmentEnvironment)
                {
                    _clientOptions.HttpClientFactory = () =>
                                                       {
                                                           HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                                                           {
                                                               ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                                                           };
                                                           return new HttpClient(httpMessageHandler);
                                                       };
                }
            }

            _databaseId = _config.DatabaseId;
            _containerPrefix = string.IsNullOrWhiteSpace(_config.ContainerPrefix) ? "" : _config.ContainerPrefix + "_";

            _client = new(_endpoint, _authKey, _clientOptions);

            GetOrCreateDatabaseAsync().Wait();

            ReseedDataAsync().Wait();
        }

        private static ContainerProperties NewContainerProperties(string containerNameWithPrefix, string partitionPath)
        {
            var props = new ContainerProperties(containerNameWithPrefix, partitionPath)
            {
                IndexingPolicy = new()
                {
                    IndexingMode = IndexingMode.Consistent
                }
            };

            return props;
        }

        private bool ContainerExits(string containerNameWithPrefix)
        {
            bool exists = true;

            var container = _db.GetContainer(containerNameWithPrefix);
            try
            {
                var props = container.ReadContainerAsync().Result;
            }
            catch (Exception ex)
            {
                if (ex.InnerException is CosmosException { StatusCode: HttpStatusCode.NotFound })
                    exists = false;
                else
                {
                    ex.FilterRelevantStackTrace();
                    throw;
                }
            }
            return exists;
        }

        /// <inheritdoc/>
        public override async Task<bool> ReseedDataAsync(string dataSetType = "Unit", bool force = false)
        {
            if (_db is null)
                return true;

            if (IsReseeding)
            {
                WorkBench.ConsoleWriteHighlightedLine($"A reseed operation is ALREADY RUNNING. Wait for it to complete before starting a new one.");
                return true;
            }

            WorkBench.ConsoleClear();

            WorkBench.ConsoleWriteLine("Repository setup: ");
            WorkBench.ConsoleWriteHighlighted(dataSetType.ToUpper());
            WorkBench.ConsoleWrite($" [{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC]\n");
            WorkBench.ConsoleWriteHighlightedLine("----------------------------------------------------------------------------\n");

            bool appendOnly = !(WorkBench.IsDevelopmentEnvironment || WorkBench.IsIntegrationEnvironment || WorkBench.IsQualityEnvironment);
            bool reseededOnce = false;
            string[] entityNames;

            try
            {
                IsReseeding = true;
                if (force)
                {
                    entityNames = GetEntityNamesFromReseedFiles();
                }
                else
                {
                    dataSetType = WorkBench.IsDevelopmentEnvironment ? "Unit"
                                : WorkBench.IsIntegrationEnvironment ? "Integration"
                                : WorkBench.IsQualityEnvironment ? "System"
                                : WorkBench.IsDemonstrationEnvironment ? "Demonstration"
                                : WorkBench.IsProductionEnvironment ? "Production"
                                : throw new LightException($"Default reseed data type not implemented for `{WorkBench.EnvironmentName}` environment");

                    entityNames = GetEntityNamesFromLightModelTypes();
                }

                foreach (var entityName in entityNames)
                {
                    string containerNameWithPrefix = _containerPrefix + entityName;
                    string partitionPath = GetPartitionPath(entityName);

                    bool isAnalyticalSource = IsAnalyticalSource(entityName);

                    bool isAutomaticallyCreatable = !IsScriptDefined(entityName);

                    bool containerExists = ContainerExits(containerNameWithPrefix);

                    if (!containerExists || force || appendOnly)
                    {
                        bool collectionWasDeleted = false;
                        if (containerExists && !appendOnly && isAutomaticallyCreatable)
                        {
                            await _db.GetContainer(containerNameWithPrefix).DeleteContainerAsync();
                            collectionWasDeleted = true;
                        }

                        if (!isAutomaticallyCreatable)
                        {
                            if (!containerExists)
                            {
                                WorkBench.ConsoleWriteErrorLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] Container '{containerNameWithPrefix}' CANNOT BE CREATED programmatically in CosmosDb database '{_databaseId}' because it is a SCRIPT DEFINED container.");
                                WorkBench.ConsoleWriteHighlightedLine($"                        It must be created from Azure Portal with its specific configuration properties before calling this operation'.");
                                WorkBench.ConsoleWriteLine();
                                return reseededOnce;
                            }
                            else
                                await ClearContainer(containerNameWithPrefix, partitionPath, dataSetType);
                        }
                        else if (!containerExists || collectionWasDeleted)
                        {
                            var newContainer = _db.CreateContainerAsync(NewContainerProperties(containerNameWithPrefix, partitionPath)).Result;

                            string actionDone = containerExists ? "RECREATED" : "CREATED";
                            WorkBench.ConsoleWriteLine();
                            WorkBench.ConsoleWrite("----------------------------------------------------------------------------\n Container ");
                            WorkBench.ConsoleWriteHighlighted($"'{containerNameWithPrefix}'");
                            WorkBench.ConsoleWrite($" (partition: '{partitionPath}') was ");
                            WorkBench.ConsoleWriteHighlighted(actionDone);
                            WorkBench.ConsoleWrite($" in CosmosDb database '{_databaseId}'.");
                            WorkBench.ConsoleWrite("\n----------------------------------------------------------------------------\n");
                            WorkBench.ConsoleWriteLine();
                        }

                        JsonDocument json = JsonDocument.Parse("[]");
                        try
                        {
                            json = GetSeedData(entityName, dataSetType);

                            if (json is null)
                                return reseededOnce;
                        }
                        catch (Exception ex)
                        {
                            WorkBench.ConsoleWriteErrorLine($"Seed data json file '{entityName}' is malformed and could not be parsed");
                            WorkBench.ConsoleWriteHighlightedLine(ex.Message);
                            WorkBench.ConsoleWriteLine();
                            break;
                        }

                        JsonElement.ArrayEnumerator arrayOfObjects;
                        try
                        {
                            arrayOfObjects = json.RootElement.EnumerateArray();
                        }
                        catch
                        {
                            WorkBench.ConsoleWriteLine();
                            WorkBench.ConsoleWriteErrorLine($"Seed data json file '{entityName}' is not a array of json");
                            WorkBench.ConsoleWriteLine();
                            break;
                        }

                        bool empty = true;
                        if (arrayOfObjects.Any())
                        {
                            var attachments = await ReseedAttachmentsAsync(dataSetType, entityName);

                            if (await InsertDataAsync(arrayOfObjects, attachments, GetEntityType(entityName), containerNameWithPrefix, dataSetType, appendOnly))
                            {
                                empty = false;
                                reseededOnce = true;
                            }
                        }

                        if (empty)
                        {
                            WorkBench.ConsoleWriteLine();
                            WorkBench.ConsoleWriteHighlightedLine($"Seed data json file '{entityName}' is an empty array");
                            WorkBench.ConsoleWriteLine();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WorkBench.ConsoleWriteLine(e.Message);

                e.FilterRelevantStackTrace();
                throw;
            }
            finally
            {
                IsReseeding = false;
            }
            return reseededOnce;
        }

        private async Task ClearContainer(string containerNameWithPrefix, string partitionPath, string dataSetType)
        {
            int records = 0;

            WorkBench.ConsoleWriteLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] Starting the record cleaning for dataset '{dataSetType}' from entity '{containerNameWithPrefix}'");

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            string nextContinuationToken = null;
            do
            {
                FeedIterator<JsonElement> setIterator = container.GetItemQueryIterator<JsonElement>("SELECT * FROM c", nextContinuationToken);

                if (setIterator.HasMoreResults)
                {
                    FeedResponse<JsonElement> page = setIterator.ReadNextAsync().Result;

                    if (page.Count > 0)
                        nextContinuationToken = page.ContinuationToken;

                    foreach (JsonElement item in page.Resource)
                    {
                        string id = item.Property("id").AsString();
                        string partitionId = item.Property(partitionPath[1..]).AsString();
                        await container.DeleteItemAsync<JsonDocument>(id, new PartitionKey(partitionId));
                        records++;
                    }
                }
            }
            while (nextContinuationToken is not null);

            WorkBench.ConsoleWriteLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] Container '{containerNameWithPrefix}' (partition: '{partitionPath}') of CosmosDb database '{_databaseId}' has got {records} RECORDS CLEARED.");
        }

        /// <inheritdoc/>
        public override void SetMediaStorage(ILightMediaStorage mediaStorage)
        {
            _mediaStorage = mediaStorage;
        }

        /// <inheritdoc/>
        public override async Task<T> GetByIdAsync<T>(string id, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                return default;

            await base.GetByIdAsync<T>(id, partitionKey);

            return await GetEntityByIdAsync<T>(id, typeof(T).Name, partitionKey);
        }

        /// <inheritdoc/>
        public override async Task<T> AddAsync<T>(T model)
        {
            return await TryAddAsync(model, 1);
        }

        private async Task<T> TryAddAsync<T>(T model, int retry) where T : ILightModel
        {
            const int MAX_RETRIES = 5;
            var type = typeof(T);

            string entityName = type.Name;
            string containerNameWithPrefix = _containerPrefix + entityName;

            bool isShortId = SetIfShortId(model);

            SetIfAutoPartition(model);

            await base.AddAsync(model);

            try
            {
                var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

                T createdEntity = await container.CreateItemAsync(model, requestOptions: RequestCustomOptions(model));

                if (IsAnalyticalSource<T>())
                    _ = Task.Factory.StartNew(() => SendModelAsDataEventAsync(containerNameWithPrefix, model, LightDataEventCMD.Insert));

                return createdEntity;
            }
            catch (CosmosException docEx)
            {
                if (docEx.StatusCode == HttpStatusCode.Conflict)
                    if (isShortId && retry <= MAX_RETRIES)
                        return await TryAddAsync(model, ++retry);
                    else
                        throw new DuplicatedInsertionLightException(entityName);
                else
                {
                    docEx.FilterRelevantStackTrace();
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public override async Task<T> UpdateAsync<T>(T model)
        {
            const int MIN_RETRY = 1;
            const int MAX_RETRIES = 3;

            await base.UpdateAsync(model);

            string entityName = typeof(T).Name;
            string containerNameWithPrefix = _containerPrefix + entityName;

            int retry = MIN_RETRY;

            do
            {
                try
                {
                    var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

                    T upsertedEntity = await container.UpsertItemAsync(model, requestOptions: RequestCustomOptions(model));

                    if (IsAnalyticalSource<T>())
                    {
                        _ = Task.Factory.StartNew(() => SendModelAsDataEventAsync(containerNameWithPrefix, model, LightDataEventCMD.Update));
                    }
                    return upsertedEntity;
                }
                catch (CosmosException e)
                {
                    if (e.StatusCode == HttpStatusCode.NotFound)
                        return default;
                    else if (e.StatusCode == HttpStatusCode.PreconditionFailed)
                        throw new OptimisticConcurrencyLightException(typeof(T).Name);
                    else if ((int)e.StatusCode == 449 || e.Message.Contains("Conflicting request to resource has been attempted. Retry to avoid conflicts."))
                    {
                        retry++;

                        if (retry > MAX_RETRIES)
                        {
                            e.FilterRelevantStackTrace();
                            throw;
                        }

                        Thread.Sleep(1000 * retry * retry * retry);
                    }
                    else
                    {
                        e.FilterRelevantStackTrace();
                        throw;
                    }
                }
            } while (retry <= MAX_RETRIES);

            return default;
        }

        /// <inheritdoc/>
        public override async Task<IEnumerable<T>> AddAsync<T>(List<T> listModels)
        {
            if (listModels is null)
                return default;

            List<T> insertedOnes = [];

            foreach (var model in listModels)
            {
                var inserted = await AddAsync(model);
                insertedOnes.Add(inserted);
            }
            return insertedOnes;
        }

        /// <inheritdoc/>
        public override async Task<IEnumerable<T>> UpdateAsync<T>(List<T> listModels)
        {
            if (listModels is null)
                return default;

            List<T> updatedOnes = [];

            foreach (var model in listModels)
            {
                var updated = await UpdateAsync(model);
                updatedOnes.Add(updated);
            }
            return updatedOnes;
        }

        private static LightAttachment MakeLightAttachment(string entityId, string fileName, string entityName, string partitionKey, Stream content)
        {
            LightAttachment lightAttachment = new()
            {
                Id = fileName,
                Name = fileName,
                MediaStream = content,
                MediaLink = fileName,
                ResourceId = ResourceIdFrom(entityId, partitionKey, entityName),
                ContentType = MimeMapping.MimeUtility.GetMimeMapping(fileName)
            };

            lightAttachment.MediaStream.Position = 0;

            return lightAttachment;
        }

        private static LightAttachment ConvertLightAttachment(string fileName, string entityName)
        {
            LightAttachment lightAttachment = new()
            {
                Id = fileName,
                Name = fileName,
                MediaLink = fileName,
                ContentType = MimeMapping.MimeUtility.GetMimeMapping(fileName),
                ResourceId = entityName + "/" + fileName
            };
            return lightAttachment;
        }

        /// <inheritdoc/>
        public override async Task<IEnumerable<ILightAttachment>> ListAttachmentsByIdAsync<T>(string entityId, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.ListAttachmentsByIdAsync<T>(entityId, partitionKey);

            T doc = await GetEntityByIdAsync<T>(entityId, typeof(T).Name, partitionKey);

            List<ILightAttachment> list = [];
            if (doc?.Attachments is not null)
                list = doc.Attachments.Select(a => new LightAttachment() { Id = a, Name = a } as ILightAttachment).ToList();

            return list;
        }

        /// <inheritdoc/>
        public override async Task<ILightAttachment> GetAttachmentAsync<T>(string entityId, string fileName, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.GetAttachmentAsync<T>(entityId, fileName, partitionKey);

            ILightAttachment attachment;
            if (_mediaStorage is not null)
            {
                string entityName = typeof(T).Name;
                T doc = await GetEntityByIdAsync<T>(entityId, entityName, partitionKey);

                var attachmentFile = doc?.Attachments?.FirstOrDefault(a => a == fileName);
                if (attachmentFile is null)
                    return null;
                else
                {
                    attachment = ConvertLightAttachment(attachmentFile, entityName);
                    ILightAttachment storage = await _mediaStorage.GetAsync(ResourceIdFrom(entityId, partitionKey, entityName), fileName);
                    attachment.MediaStream = storage.MediaStream;
                    attachment.MediaStream.Position = 0;
                }
            }
            else
                throw new LightException("media storage was not injected and configured");

            return attachment;
        }

        /// <inheritdoc/>
        public override async Task<Stream> GetAttachmentBlockAsync<T>(string entityId, string fileName, int blockNumber, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.GetAttachmentBlockAsync<T>(entityId, fileName, blockNumber, partitionKey); //Calls the base class because there may be some generic behavior in it

            string entityName = typeof(T).Name;

            if (_mediaStorage is not null)
                return await _mediaStorage.GetBlockAsync(ResourceIdFrom(entityId, partitionKey, entityName), fileName, blockNumber);
            else
                throw new LightException("media storage was not injected and configured");
        }

        /// <inheritdoc/>
        public override async Task<ILightAttachment> SaveAttachmentAsync<T>(string entityId, string fileName, Stream attachment, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.SaveAttachmentAsync<T>(entityId, fileName, attachment, partitionKey); //Calls the base class because there may be some generic behavior in it

            string entityName = typeof(T).Name;

            T doc = await GetEntityByIdAsync<T>(entityId, entityName, partitionKey);
            if (doc is null)
                return null;

            ILightAttachment upserted = MakeLightAttachment(entityId, fileName, entityName, partitionKey, attachment);

            if (_mediaStorage is not null)
            {
                await _mediaStorage.InsertUpdateAsync(upserted);

                if (doc.Attachments?.Any(a => a == upserted.Id) != true)
                {
                    doc.Attachments ??= [];

                    doc.Attachments.Add(upserted.Id);
                    doc = await UpdateAsync(doc);

                    string eTag = (string)doc.GetType().GetProperty("ETag")?.GetValue(doc, null);
                    if (eTag is not null)
                        upserted.ETag = eTag;
                }

                upserted.Attachments = doc.Attachments;
            }
            else
                throw new LightException("media storage was not injected and configured");

            return upserted;
        }

        /// <inheritdoc/>
        public override async Task<ILightAttachment> ReplaceAttachmentAsync<T>(string entityId, string fileName, Stream attachment, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.ReplaceAttachmentAsync<T>(entityId, fileName, attachment, partitionKey); //Calls the base class because there may be some generic behavior in it

            string entityName = typeof(T).Name;

            T doc = await GetEntityByIdAsync<T>(entityId, entityName, partitionKey);
            if (doc is null)
                return null;

            ILightAttachment upserted = MakeLightAttachment(entityId, fileName, entityName, partitionKey, attachment);

            if (!doc.Attachments.Any(a => a == upserted.Id))
                throw new LightException("no attachment found to replace");

            if (_mediaStorage is not null)
                await _mediaStorage.ReplaceAsync(upserted);
            else
                throw new LightException("media storage was not injected and configured");

            return upserted;
        }

        /// <inheritdoc/>
        public override async Task AppendToAttachmentAsync<T>(string entityId, string fileName, Stream block, int blockNumber, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.AppendToAttachmentAsync<T>(entityId, fileName, block, blockNumber, partitionKey); //Calls the base class because there may be some generic behavior in it

            string entityName = typeof(T).Name;

            if (_mediaStorage is not null)
                await _mediaStorage.AppendBlockAsync(ResourceIdFrom(entityId, partitionKey, entityName), fileName, block, blockNumber);
            else
                throw new LightException("media storage was not injected and configured");
        }

        /// <inheritdoc/>
        public override async Task<ILightAttachment> DeleteAttachmentAsync<T>(string entityId, string fileName, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            await base.DeleteAttachmentAsync<T>(entityId, fileName, partitionKey);

            var entityName = typeof(T).Name;

            T doc = await GetEntityByIdAsync<T>(entityId, entityName, partitionKey);

            if (doc is null)
                return null;

            LightAttachment deleted = new()
            {
                Id = fileName,
                ResourceId = ResourceIdFrom(entityId, partitionKey, entityName)
            };

            bool wasDeleted = false;
            if (doc.Attachments?.Any(a => a == fileName) == true)
                if (_mediaStorage is not null)
                {
                    doc.Attachments.RemoveAll(a => a == fileName);
                    doc = await UpdateAsync(doc);

                    wasDeleted = true;

                    string eTag = (string)doc.GetType().GetProperty("ETag")?.GetValue(doc, null);
                    if (eTag is not null)
                        deleted.ETag = eTag;

                    _ = _mediaStorage.Remove(deleted);
                }
                else
                    throw new LightException("media storage was not injected and configured");

            deleted.Attachments = doc.Attachments;

            return wasDeleted ? deleted : null;
        }

        /// <inheritdoc/>
        public override async Task<long> CountAsync<T>()
        {
            await base.CountAsync<T>();
            return await CountAsync<T>(_ => true);
        }

        /// <inheritdoc/>
        public override async Task<long> CountAsync<T>(Expression<Func<T, bool>> filter)
        {
            await base.CountAsync(filter); //Calls the base class because there may be some generic behavior in it

            string entityName = typeof(T).Name;
            string containerNameWithPrefix = _containerPrefix + entityName;

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            var interator = container.GetItemLinqQueryable<T>(linqSerializerOptions: LightRepositorySerialization.LinqDefault)
                                     .Where(filter)
                                     .ToFeedIterator();

            var count = 0;

            while (interator.HasMoreResults)
            {
                var response = await interator.ReadNextAsync();
                count += response.Resource.Count();
            }

            return count;
        }

        /// <inheritdoc/>
        public override async Task<T> DeleteAsync<T>(string entityId, string partitionKey = null)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            string entityName = typeof(T).Name;
            string containerNameWithPrefix = _containerPrefix + entityName;

            await base.DeleteAsync<T>(entityId, partitionKey);

            try
            {
                var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

                T toDelete = await container.ReadItemAsync<T>(entityId, new PartitionKey(partitionKey));

                if (toDelete is not null)
                {
                    if (_mediaStorage is not null)
                    {
                        foreach (string attachment in toDelete?.Attachments ?? [])
                            _ = _mediaStorage.Remove(new LightAttachment()
                            {
                                Id = attachment,
                                ResourceId = ResourceIdFrom(entityId, partitionKey, entityName)
                            });
                    }

                    T deleted = await container.DeleteItemAsync<T>(entityId, new PartitionKey(partitionKey));

                    if (IsAnalyticalSource<T>())
                    {
                        // Call LightEvent to raise events from LightModel's attributes
                        _ = Task.Factory.StartNew(() => SendModelAsDataEventAsync(_containerPrefix + entityName, deleted, LightDataEventCMD.Delete));
                    }

                    return deleted;
                }
            }
            catch (Exception e)
            {
                if (e.InnerException is CosmosException ex)
                {
                    if (ex.StatusCode != HttpStatusCode.NotFound)
                    {
                        e.FilterRelevantStackTrace();
                        throw;
                    }
                }
                else
                {
                    e.FilterRelevantStackTrace();
                    throw;
                }
            }

            return default;
        }

        /// <inheritdoc/>
        public override IEnumerable<T> Get<T>(Expression<Func<T, bool>> filter, Expression<Func<T, object>> orderBy = null, bool descending = false)
        {
            //Forces the result as Enumerable due to an inconsistent behavior of enumerable results from CosmosDB
            return InternalGet(filter, orderBy, descending).AsEnumerable();
        }

        /// <inheritdoc/>
        public override IEnumerable<T> GetAll<T>(Expression<Func<T, object>> orderBy = null, bool descending = false)
        {
            base.GetAll(orderBy);
            return Get(_ => true, orderBy, descending);
        }

        /// <inheritdoc/>
        public override async Task<ILightPaging<T>> GetByPageAsync<T>(Expression<Func<T, bool>> filter, ILightPagingParms pagingParms, Expression<Func<T, object>> orderBy = null, bool descending = false)
        {
            var timeoutAt = WorkBench.UtcNow.AddSeconds(TIMEOUT_IN_SECONDS);

            await base.GetByPageAsync(filter, pagingParms, orderBy); //Calls the base class because there may be some generic behavior in it

            pagingParms ??= LightPagingParms.Default;
            if (pagingParms.ItemsPerPage <= 0)
                pagingParms.ItemsPerPage = LightPagingParms.Default.ItemsPerPage;

            QueryRequestOptions options = new() { MaxItemCount = pagingParms.ItemsPerPage };

            string entityName = typeof(T).Name;
            string containerNameWithPrefix = _containerPrefix + entityName;

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            string requestContinuation = string.IsNullOrWhiteSpace(pagingParms.ContinuationToken)
                        ? null
                        : Encoding.UTF8.GetString(Convert.FromBase64String(pagingParms.ContinuationToken));

            IQueryable<T> query;

            try
            {
                if (orderBy is null)
                    query = container.GetItemLinqQueryable<T>(false, requestContinuation, options, LightRepositorySerialization.LinqDefault)
                                     .Where(filter);
                else if (descending)
                    query = container.GetItemLinqQueryable<T>(false, requestContinuation, options, LightRepositorySerialization.LinqDefault)
                                     .Where(filter)
                                     .OrderByDescending(orderBy);
                else
                    query = container.GetItemLinqQueryable<T>(false, requestContinuation, options, LightRepositorySerialization.LinqDefault)
                                     .Where(filter)
                                     .OrderBy(orderBy);

                List<T> data = ReadPageFeedIterator(query.ToFeedIterator(), out string nextContinuationToken, timeoutAt);

                LightPaging<T> result = new()
                {
                    Data = data,
                    ItemsPerPage = pagingParms.ItemsPerPage,
                    ContinuationToken = string.IsNullOrWhiteSpace(nextContinuationToken)
                                            ? null
                                            : Convert.ToBase64String(Encoding.UTF8.GetBytes(nextContinuationToken))
                };

                return await Task.FromResult<ILightPaging<T>>(result);
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
            {
                throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
            }
            catch (Exception ex) when (ex is OperationCanceledException ||
                                             (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
            {
                throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
            }
            catch (Exception ex)
            {
                ex.FilterRelevantStackTrace();

                if (ex.InnerException?.GetType() == typeof(CosmosException))
                {
                    var cosmosEx = ex.InnerException as CosmosException;

                    if (cosmosEx.StatusCode == HttpStatusCode.BadRequest)
                        throw new LightException($"Bad request for CosmosDb: {cosmosEx.InnerException?.Message}");
                }

#pragma warning disable CA2200 // Rethrow to preserve stack details
                throw ex;
#pragma warning restore CA2200 // Rethrow to preserve stack details
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<T> Query<T>(string query)
        {
            return InternalQuery<T>(query).AsEnumerable();
        }

        /// <inheritdoc/>
        public override async Task<IEnumerable<T>> QueryAsync<T>(string query)
        {
            return (await InternalQueryAsync<T>(query)).AsEnumerable();
        }

        /// <inheritdoc/>
        public override async Task<ILightPaging<T>> QueryByPageAsync<T>(string query, ILightPagingParms pagingParms)
        {
            var timeoutAt = WorkBench.UtcNow.AddSeconds(TIMEOUT_IN_SECONDS);

            pagingParms ??= LightPagingParms.Default;
            if (pagingParms.ItemsPerPage <= 0)
                pagingParms.ItemsPerPage = LightPagingParms.Default.ItemsPerPage;

            string entityName = GetContainerName(query);
            string containerNameWithPrefix = _containerPrefix + entityName;

            var type = await Task.Run(() => GetEntityType(entityName))
                         ?? throw new LightException($"The collection name '{entityName}' does not match any defined LightModel type or subtype. " +
                                                     $"Check query entity reference and/or model types.");

            //A way to call base's overriden generic method by generics from the overriding method couldn't be found.
            //So the following reflection method call replaces the standard call to the base overriden method as other methods do.
            //base.Query<T>(query); 
            base.GetType().GetMethod("CheckSetup").MakeGenericMethod([type]).Invoke(this, []);

            QueryRequestOptions options = new() { MaxItemCount = pagingParms.ItemsPerPage };

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            string requestContinuation = string.IsNullOrWhiteSpace(pagingParms.ContinuationToken)
                                            ? null
                                            : Encoding.UTF8.GetString(Convert.FromBase64String(pagingParms.ContinuationToken));

            try
            {
                FeedIterator<T> setIterator = container.GetItemQueryIterator<T>(ExpandCollectionReference(query, entityName), requestContinuation, options);

                List<T> data = ReadPageFeedIterator(setIterator, out string nextContinuationToken, timeoutAt);

                LightPaging<T> result = new()
                {
                    Data = data,
                    ItemsPerPage = pagingParms.ItemsPerPage,
                    ContinuationToken = string.IsNullOrWhiteSpace(nextContinuationToken)
                                ? null
                                : Convert.ToBase64String(Encoding.UTF8.GetBytes(nextContinuationToken))
                };

                return result;
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
            {
                throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
            }
            catch (Exception ex) when (ex is OperationCanceledException ||
                                             (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
            {
                throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
            }
            catch (Exception ex)
            {
                ex.FilterRelevantStackTrace();

                if (ex.InnerException?.GetType() == typeof(CosmosException))
                {
                    var cosmosEx = ex.InnerException as CosmosException;
                    ex.FilterRelevantStackTrace();
                    if (cosmosEx.StatusCode == HttpStatusCode.BadRequest)
                        throw new LightException($"Bad request for CosmosDb: {cosmosEx.InnerException?.Message}");
                }

#pragma warning disable CA2200 // Rethrow to preserve stack details
                throw ex;
#pragma warning restore CA2200 // Rethrow to preserve stack details
            }
        }

        private static string ExpandCollectionReference(string query, string entityName)
        {
            query = ReferenceRegex().Replace(query, " ");

            query = query.Replace("> as", ">");
            query = query.Replace("> AS", ">");
            query = query.Replace("> As", ">");
            query = query.Replace("> aS", ">");

            return query.Replace($"<{entityName}>", "");
        }

        private static string GetContainerName(string statement)
        {
            var tableRefs = NameRegex().Matches(statement).Select(m => m.Value).ToList();

            if (tableRefs.Count == 0)
                throw new LightException($"No collection reference in the FROM clause (such as 'FROM <name-of-collection> AS c') was found in the query: {statement}");
            else if (tableRefs.Count > 1)
                throw new LightException($"{tableRefs.Count} collection references (such as '<name-of-collection>') were found. Only one is allowed per query: {statement}");

            return tableRefs.FirstOrDefault()[1..^1];
        }

        /// <inheritdoc/>
        public override async Task CommandAsync(string command)
        {
            await Task.Run(() => { throw new NotImplementedException("CosmosDB is not an SQL database, thus it does not support DML commands."); });
        }

        /// <inheritdoc/>
        public override string SlidingUpsertSql(string selectedColumns, Type eventTable, string partitionColumns, DateTime fromDateTime, DateTime toDateTime)
        {
            throw new NotImplementedException("CosmosDB is not an SQL database, thus it does not support Analytical SQL queries.");
        }

        /// <inheritdoc/>
        public override string SlidingDeleteSql(Type eventTable, string partitionColumns, DateTime fromDateTime, DateTime toDateTime)
        {
            throw new NotImplementedException("CosmosDB is not an SQL database, thus it does not support Analytical SQL queries.");
        }

        /// <inheritdoc/>
        public override string SlidingDeleteSql(string selectedColumns, Type eventTable, string partitionColumns, DateTime fromDateTime, DateTime toDateTime)
        {
            throw new NotImplementedException("CosmosDB is not an SQL database, thus it does not support Analytical SQL queries.");
        }

        /// <inheritdoc/>
        public override string SlidingAllSql(Type eventTable, string partitionColumns, DateTime fromDateTime, DateTime toDateTime)
        {
            throw new NotImplementedException("CosmosDB is not an SQL database, thus it does not support Analytical SQL queries.");
        }

        /// <inheritdoc/>
        public override string SlidingAllSql(string selectedColumns, Type eventTable, string partitionColumns, DateTime fromDateTime, DateTime toDateTime)
        {
            throw new NotImplementedException("CosmosDB is not an SQL database, thus it does not support Analytical SQL queries.");
        }

        /// <inheritdoc/>
        public override LightHealth.HealthCheckStatus HealthCheck(string serviceKey, string value)
        {
            try
            {
                var x = _client.GetDatabase(_databaseId);
                return LightHealth.HealthCheckStatus.Healthy;
            }
            catch
            {
                return LightHealth.HealthCheckStatus.Unhealthy;
            }
        }

        private static ItemRequestOptions RequestCustomOptions<T>(T model)
        {
            if (IsOptimisticConcurrency(typeof(T)))
            {
                object eTag = model.GetType().BaseType.GetProperty("ETag").GetValue(model);

                //Check obj is different null for convert to string.
                if (eTag is not null)
                {
                    return new ItemRequestOptions()
                    {
                        IfMatchEtag = (string)eTag
                    };
                }
            }
            return new ItemRequestOptions();
        }

        private async Task GetOrCreateDatabaseAsync()
        {
            Database database = _client.GetDatabase(_databaseId);
            bool exists = true;
            try
            {
                var props = database.ReadAsync().Result;
            }
            catch (Exception ex)
            {
                if (ex.InnerException is CosmosException { StatusCode: HttpStatusCode.NotFound })
                    exists = false;
                else
                {
                    ex.FilterRelevantStackTrace();
                    throw;
                }
            }

            if (!exists)
            {
                if (_config.CreateIfNotExists)
                {
                    try
                    {
                        database = await _client.CreateDatabaseAsync(_databaseId, ThroughputProperties.CreateManualThroughput(_config.DatabaseRUs));
                    }
                    catch (CosmosException ce)
                    {
                        if (ce.StatusCode == HttpStatusCode.BadRequest)
                        {
                            database = await _client.CreateDatabaseAsync(_databaseId);
                        }
                    }

                    WorkBench.ConsoleWriteLine($"CosmosDb database '{_databaseId}' created.");
                }
                else
                {
                    throw new LightException($"DatabaseId '{_databaseId}' on CosmosDb settings does not exists in Database [{_config.Endpoint}].");
                }
            }

            WorkBench.WriteLog(); //Ignores console log for this reseed

            _db = database;
        }

        private async Task<T> GetEntityByIdAsync<T>(string entityId, string entityName, string partitionKey = null) where T : ILightModel
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                partitionKey = GetDefaultPartition<T>(entityId);

            string containerNameWithPrefix = _containerPrefix + entityName;

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            T item = default;

            try
            {
                item = await container.ReadItemAsync<T>(entityId, new PartitionKey(partitionKey));
            }
            catch (AggregateException ae)
            {
                bool notFound = false;
                foreach (var e in ae.Flatten().InnerExceptions)
                {
                    if (e is CosmosException docexp && docexp.StatusCode == HttpStatusCode.NotFound)
                    {
                        notFound = true;
                    }
                }
                if (!notFound)
                {
                    ae.FilterRelevantStackTrace();
                    throw;
                }
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode != HttpStatusCode.NotFound)
                {
                    ex.FilterRelevantStackTrace();
                    throw;
                }
            }

            return item;
        }

        private async Task<bool> InsertDataAsync(JsonElement.ArrayEnumerator initialData, Dictionary<string, List<string>> attachmentsById, Type entityType, string collectionNameWithPrefix, string dataSetType, bool appendOnly)
        {
            if (entityType is null)
            {
                WorkBench.ConsoleWriteErrorLine($"The seed file does not have a corresponding LightModel class. CHECK NAMES AND CASES.");
                return false;
            }

            bool success = true;
            int records = 0;

            WorkBench.ConsoleWriteLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] Found {initialData.Count()} record(s) from dataset '{dataSetType}' to be seeded into entity '{collectionNameWithPrefix}'");

            var container = _db.GetContainer(collectionNameWithPrefix);

            ILightModel entity = default;
            JsonElement lastSuccessfulRecord = default;
            try
            {
                foreach (var record in initialData)
                {
                    lastSuccessfulRecord = record;
                    entity = record.Deserialize(entityType, LightRepositorySerialization.JsonDefault) as ILightModel;

                    if (entity.Id is null)
                        throw new Exception("Id property not found in the record");

                    if (attachmentsById?.ContainsKey(entity.Id) == true)
                        entity.Attachments = attachmentsById[entity.Id];

                    ValidateAndCheck(entity);

                    var model = Convert.ChangeType(entity, entityType);
                    SetIfAutoPartition(entityType, model);

                    var created = await container.CreateItemAsync(model, requestOptions: RequestCustomOptions(entityType));
                    records++;
                }
            }
            catch (CosmosException)
            {
                // a conflict means that the seed data is already inserted
                WorkBench.ConsoleWriteErrorLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] ID conflict at record #{records + 1} (Id=\"{entity.Id}\")");
                success = false;
            }
            catch (InvalidModelLightException e)
            {
                // returns false indicating that the seeding was not complete
                success = false;
                WorkBench.ConsoleWriteErrorLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] VALIDATION FAILURE at record #{records + 1} (Id=\"{entity.Id}\"): {string.Join(", ", e.InputErrors.Select(e => e.Code).ToArray())}");
            }
            catch (Exception e)
            {
                WorkBench.ConsoleWriteErrorLine($"JSON failed to deserialize to '{entityType.Name}':");
                WorkBench.ConsoleWriteErrorLine(lastSuccessfulRecord.ToJsonString(true));
                WorkBench.ConsoleWriteErrorLine($"Exception: {e.Message}");
                success = false;
            }

            string successMessage = success ? "SUCCESSFULLY inserted" : "was STOPPED due to Id conflict AND/OR data validation failure and ONLY inserted";
            string recordMessage = records > 1 ? "records" : "record";
            string logMessage = $"Seeding process {successMessage} {records} {recordMessage} from dataset '{dataSetType}' into entity '{collectionNameWithPrefix}'";

            if (appendOnly && !success)
            {
                // A 0 record insertion means that another instance (another K8s pod) had already seeded (appended) the dataset.
                // If the dataset was partially inserted it means that it has inconsistences.
                if (records > 0)
                {
                    logMessage += $". Check the dataset (seed file) and the data already inserted in the DB.";
                    WorkBench.BaseTelemetry.TrackException(new LightException(logMessage));
                    WorkBench.ConsoleWriteLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] {logMessage}");
                }
                else
                    WorkBench.ConsoleWriteHighlightedLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] Records had already been seeded. Remove the dataset seed file in next release so to stop seeing this message.");
            }
            else
                WorkBench.ConsoleWriteLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] {logMessage}");

            WorkBench.ConsoleWriteLine();

            return success;
        }

        private IEnumerable<T> InternalGet<T>(Expression<Func<T, bool>> filter, Expression<Func<T, object>> orderBy, bool descending) where T : ILightModel
        {
            var timeoutAt = WorkBench.UtcNow.AddSeconds(TIMEOUT_IN_SECONDS);

            base.Get(filter, orderBy);

            QueryRequestOptions options = new() { MaxItemCount = -1 };

            IQueryable<T> query;

            string entityName = typeof(T).Name;
            string containerNameWithPrefix = _containerPrefix + entityName;

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            string nextContinuationToken = null;
            do
            {
                FeedIterator<T> setIterator;
                try
                {
                    if (orderBy is null)
                        query = container.GetItemLinqQueryable<T>(false, nextContinuationToken, requestOptions: options, LightRepositorySerialization.LinqDefault)
                                         .Where(filter);
                    else if (descending)
                        query = container.GetItemLinqQueryable<T>(false, nextContinuationToken, requestOptions: options, LightRepositorySerialization.LinqDefault)
                                         .Where(filter)
                                         .OrderByDescending(orderBy);
                    else
                        query = container.GetItemLinqQueryable<T>(false, nextContinuationToken, requestOptions: options, LightRepositorySerialization.LinqDefault)
                                         .Where(filter)
                                         .OrderBy(orderBy);

                    setIterator = query.ToFeedIterator();
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                    (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                {
                    throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                }
                catch (Exception ex) when (ex is OperationCanceledException ||
                                                 (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                {
                    throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                }

                if (setIterator?.HasMoreResults == true)
                {
                    var utcNow = WorkBench.UtcNow;

                    if (timeoutAt > utcNow)
                    {
                        CancellationTokenSource cts = new();
                        cts.CancelAfter(timeoutAt - utcNow);

                        FeedResponse<T> page;
                        try
                        {
                            page = setIterator.ReadNextAsync(cts.Token).Result;
                        }
                        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                            (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                        {
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException ||
                                                         (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                        {
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                        }

                        if (page.Count > 0)
                            nextContinuationToken = page.ContinuationToken;

                        foreach (T item in page.Resource)
                            yield return item;
                    }
                    else
                        throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond");
                }
            }
            while (nextContinuationToken is not null);
        }

        private IEnumerable<T> InternalQuery<T>(string query)
        {
            var timeoutAt = WorkBench.UtcNow.AddSeconds(TIMEOUT_IN_SECONDS);

            string entityName = GetContainerName(query);
            string containerNameWithPrefix = _containerPrefix + entityName;

            var type = GetEntityType(entityName)
                            ?? throw new LightException($"The container name '{entityName}' does not match any defined LightModel type or subtype. " +
                                                        $"Check query entity reference and/or model types.");

            //A way to call base's overriden generic method by generics from the overriding method couldn't be found.
            //So the following reflection method call replaces the standard call to the base overriden method as other methods do.
            //base.Query<T>(query); 
            base.GetType().GetMethod("CheckSetup").MakeGenericMethod([type]).Invoke(this, []);

            QueryRequestOptions options = new() { MaxItemCount = -1 };

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            string nextContinuationToken = null;
            do
            {
                FeedIterator<T> setIterator;
                try
                {
                    setIterator = container.GetItemQueryIterator<T>(ExpandCollectionReference(query, entityName), nextContinuationToken, options);
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                    (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                {
                    throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                }
                catch (Exception ex) when (ex is OperationCanceledException ||
                                                 (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                {
                    throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                }
                catch (Exception ex) when (ex is not TimeoutException)
                {
                    ex.FilterRelevantStackTrace();

                    if (ex.InnerException?.GetType() == typeof(CosmosException))
                    {
                        var cosmosEx = ex.InnerException as CosmosException;
                        ex.FilterRelevantStackTrace();
                        if (cosmosEx.StatusCode == HttpStatusCode.BadRequest)
                            throw new LightException($"Bad request for CosmosDb: {cosmosEx.InnerException?.Message}");
                    }

#pragma warning disable CA2200 // Rethrow to preserve stack details
                    throw ex;
#pragma warning restore CA2200 // Rethrow to preserve stack details
                }

                if (setIterator?.HasMoreResults == true)
                {
                    var utcNow = WorkBench.UtcNow;

                    if (timeoutAt > utcNow)
                    {
                        CancellationTokenSource cts = new();
                        cts.CancelAfter(timeoutAt - utcNow);

                        FeedResponse<T> page;
                        try
                        {
                            page = setIterator.ReadNextAsync(cts.Token).Result;
                        }
                        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                            (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                        {
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException ||
                                                         (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                        {
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                        }

                        if (page.Count > 0)
                            try
                            {
                                nextContinuationToken = page.ContinuationToken;
                            }
                            catch
                            {
                                nextContinuationToken = null;
                            }

                        foreach (T item in page.Resource)
                            yield return item;
                    }
                    else
                        throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond");
                }
            }
            while (nextContinuationToken is not null);
        }

        private async Task<IEnumerable<T>> InternalQueryAsync<T>(string query)
        {
            var timeoutAt = WorkBench.UtcNow.AddSeconds(TIMEOUT_IN_SECONDS);

            string entityName = GetContainerName(query);
            string containerNameWithPrefix = _containerPrefix + entityName;

            var type = await Task.Run(() => GetEntityType(entityName))
                         ?? throw new LightException($"The collection name '{entityName}' does not match any defined LightModel type or subtype. " +
                                                     $"Check query entity reference and/or model types.");

            //A way to call base's overriden generic method by generics from the overriding method couldn't be found.
            //So the following reflection method call replaces the standard call to the base overriden method as other methods do.
            //base.Query<T>(query); 
            base.GetType().GetMethod("CheckSetup").MakeGenericMethod([type]).Invoke(this, []);

            QueryRequestOptions options = new() { MaxItemCount = -1 };

            var container = _client.GetContainer(_databaseId, containerNameWithPrefix);

            List<T> ret = [];

            try
            {
                string nextContinuationToken = null;
                do
                {
                    FeedIterator<T> setIterator;
                    try
                    {
                        setIterator = container.GetItemQueryIterator<T>(ExpandCollectionReference(query, entityName), nextContinuationToken, options);
                    }
                    catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                        (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                    {
                        throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException ||
                                                     (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                    {
                        throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                    }

                    if (setIterator?.HasMoreResults == true)
                    {
                        var utcNow = WorkBench.UtcNow;

                        if (timeoutAt > utcNow)
                        {
                            CancellationTokenSource cts = new();
                            cts.CancelAfter(timeoutAt - utcNow);

                            FeedResponse<T> page;
                            try
                            {
                                page = setIterator.ReadNextAsync(cts.Token).Result;
                            }
                            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                                (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                            {
                                throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                            }
                            catch (Exception ex) when (ex is OperationCanceledException ||
                                                             (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                            {
                                throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                            }

                            if (page.Count > 0)
                                try
                                {
                                    nextContinuationToken = page.ContinuationToken;
                                }
                                catch
                                {
                                    nextContinuationToken = null;
                                }

                            foreach (T item in page.Resource)
                                ret.Add(item);
                        }
                        else
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond");
                    }
                }
                while (nextContinuationToken is not null);

                return ret.AsEnumerable();
            }
            catch (Exception ex) when (ex is not TimeoutException)
            {
                ex.FilterRelevantStackTrace();

                if (ex.InnerException?.GetType() == typeof(CosmosException))
                {
                    var cosmosEx = ex.InnerException as CosmosException;
                    ex.FilterRelevantStackTrace();
                    if (cosmosEx.StatusCode == HttpStatusCode.BadRequest)
                        throw new LightException($"Bad request for CosmosDb: {cosmosEx.InnerException?.Message}");
                }

#pragma warning disable CA2200 // Rethrow to preserve stack details
                throw ex;
#pragma warning restore CA2200 // Rethrow to preserve stack details
            }
        }

        private static List<T> ReadPageFeedIterator<T>(FeedIterator<T> setIterator, out string nextContinuationToken, DateTime timeoutAt)
        {
            List<T> ret = [];
            nextContinuationToken = null;

            try
            {
                if (setIterator.HasMoreResults)
                {
                    var utcNow = WorkBench.UtcNow;

                    if (timeoutAt > utcNow)
                    {
                        CancellationTokenSource cts = new();
                        cts.CancelAfter(timeoutAt - utcNow);

                        FeedResponse<T> page;
                        try
                        {
                            page = setIterator.ReadNextAsync(cts.Token).Result;
                        }
                        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException ||
                                                            (ex.InnerException is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                        {
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException ||
                                                         (ex is TaskCanceledException && timeoutAt <= WorkBench.UtcNow))
                        {
                            throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond", ex);
                        }

                        if (page.Count > 0)
                            nextContinuationToken = page.ContinuationToken;
                        ret.AddRange(page.Resource);
                    }
                    else
                        throw new TimeoutException($"CosmosDB query took more than {TIMEOUT_IN_SECONDS}s to respond");
                }
            }
            catch (CosmosException ex)
            {
                throw new LightException($"Failed to query database. Error message: {ex.Message}", ex);
            }

            return ret;
        }

        private async Task<Dictionary<string, List<string>>> ReseedAttachmentsAsync(string dataSetType, string fileEntityName)
        {
            bool mediaStoreOk = false;
            if (_mediaStorage is null)
                return null;

            try
            {
                await _mediaStorage.RemoveDirectoryAsync(fileEntityName.ToLower());
                mediaStoreOk = true;
            }
            catch
            {
                WorkBench.ConsoleWriteLine();
                WorkBench.ConsoleWriteErrorLine("Error while connecting to MediaStorage. No attachment was seed");
                if (WorkBench.IsDevelopmentEnvironment)
                {
                    WorkBench.ConsoleWriteLine();
                    WorkBench.ConsoleWriteHighlightedLine("IMPORTANT! Check if Azure Storage Emulator is up and running");
                }
            }

            if (!mediaStoreOk)
                return null;

            int records = 0;

            Dictionary<string, List<string>> inserted = [];
            foreach (var file in StubHandler.GetAttachmentFilesNames(dataSetType, fileEntityName))
            {
                string idAndPartitionKey = file.Key.Split(".")[0];
                string fileName = file.Key.Replace(idAndPartitionKey + ".", "");

                string partitionKey;
                string id;
                if (idAndPartitionKey.Contains('_'))
                {
                    partitionKey = idAndPartitionKey.Split("_")[0];
                    id = idAndPartitionKey.Split("_")[1];
                }
                else
                {
                    partitionKey = idAndPartitionKey;
                    id = idAndPartitionKey;
                }

                records++;

                var upserted = MakeLightAttachment(id, fileName, fileEntityName, partitionKey, new MemoryStream(file.Value));

                await _mediaStorage.InsertUpdateAsync(upserted);

                if (inserted.TryGetValue(id, out List<string> value))
                    value.Add(fileName);
                else
                    inserted[id] = [fileName];
            }

            if (records > 0)
            {
                string recordPart = records > 1 ? "records were" : "record was";
                WorkBench.ConsoleWriteLine($"[{WorkBench.UtcNow.ToShortDateString()} {WorkBench.UtcNow.ToLongTimeString()} UTC] {records} attachment {recordPart} seeded from dataset '{dataSetType}' into the entity '{fileEntityName}'");
            }

            return inserted;
        }

        /// <inheritdoc/>
        public override string ExpandQuery(string query)
        {
            return ExpandCollectionReference(query, GetContainerName(query));
        }

        /// <inheritdoc/>
        public override string ExpandCommand(string command)
        {
            throw new NotImplementedException("CosmosDB is not an SQL database and thus does not support DML commands.");
        }

        private static string ResourceIdFrom(string entityId, string partitionKey, string entityName)
        {
            return entityName.ToLower() + "/" +
                   (string.IsNullOrWhiteSpace(partitionKey) || partitionKey == entityId
                       ? ""
                       : partitionKey + "_") +
                   entityId;
        }

        [GeneratedRegex(@"[^\S\r\n]+")]
        private static partial Regex ReferenceRegex();
        [GeneratedRegex(@"\<(\w|-)+\>")]
        private static partial Regex NameRegex();
    }
}