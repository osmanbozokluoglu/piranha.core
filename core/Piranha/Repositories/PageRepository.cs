﻿/*
 * Copyright (c) 2016-2018 Håkan Edling
 *
 * This software may be modified and distributed under the terms
 * of the MIT license.  See the LICENSE file for details.
 * 
 * https://github.com/piranhacms/piranha.core
 * 
 */

using Microsoft.EntityFrameworkCore;
using Piranha.Data;
using Piranha.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace Piranha.Repositories
{
    public class PageRepository : IPageRepository
    {
        private readonly IDb db;
        private readonly IApi api;
        private readonly IContentService<Page, PageField, Models.PageBase> contentService;
        private readonly ICache cache;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="api">The current api</param>
        /// <param name="db">The current db context</param>
        /// <param name="factory">The content service factory</param>
        /// <param name="cache">The optional model cache</param>
        public PageRepository(IApi api, IDb db, IContentServiceFactory factory, ICache cache = null) {
            this.api = api;
            this.db = db;
            this.contentService = factory.CreatePageService();
            this.cache = cache;
        }

        /// <summary>
        /// Creates and initializes a new page of the specified type.
        /// </summary>
        /// <returns>The created page</returns>
        public T Create<T>(string typeId = null) where T : Models.PageBase {
            if (string.IsNullOrWhiteSpace(typeId))
                typeId = typeof(T).Name;

            return contentService.Create<T>(api.PageTypes.GetById(typeId));
        }

        /// <summary>
        /// Gets all of the available pages for the current site.
        /// </summary>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The pages</returns>
        public IEnumerable<Models.DynamicPage> GetAll(Guid? siteId = null) {
            return GetAll<Models.DynamicPage>(siteId);
        }

        /// <summary>
        /// Gets all of the available pages for the current site.
        /// </summary>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The pages</returns>
        public IEnumerable<T> GetAll<T>(Guid? siteId = null) where T : Models.PageBase {
            if (!siteId.HasValue) {
                var site = api.Sites.GetDefault();

                if (site != null)
                    siteId = site.Id;
            }

            var pages = db.Pages
                .AsNoTracking()
                .Where(p => p.SiteId == siteId)
                .OrderBy(p => p.ParentId)
                .ThenBy(p => p.SortOrder)
                .Select(p => p.Id);

            var models = new List<T>();

            foreach (var page in pages) {
                var model = GetById<T>(page);

                if (model != null)
                    models.Add(model);
            }
            return models;            
        }

        /// <summary>
        /// Gets the available blog pages for the current site.
        /// </summary>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The pages</returns>
        public IEnumerable<Models.DynamicPage> GetAllBlogs(Guid? siteId = null) {
            return GetAllBlogs<Models.DynamicPage>(siteId);
        }

        /// <summary>
        /// Gets the available blog pages for the current site.
        /// </summary>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The pages</returns>
        public IEnumerable<T> GetAllBlogs<T>(Guid? siteId = null) where T : Models.PageBase {
            if (!siteId.HasValue) {
                var site = api.Sites.GetDefault();

                if (site != null)
                    siteId = site.Id;
            }

            var pages = db.Pages
                .AsNoTracking()
                .Where(p => p.SiteId == siteId && p.ContentType == "Blog")
                .OrderBy(p => p.ParentId)
                .ThenBy(p => p.SortOrder)
                .Select(p => p.Id);

            var models = new List<T>();

            foreach (var page in pages) {
                var model = GetById<T>(page);

                if (model != null)
                    models.Add(model);
            }
            return models;            
        }

        /// <summary>
        /// Gets the site startpage.
        /// </summary>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The page model</returns>
        public Models.DynamicPage GetStartpage(Guid? siteId = null) {
            return GetStartpage<Models.DynamicPage>(siteId);
        }

        /// <summary>
        /// Gets the site startpage.
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <param param name="siteId">The optional site id</param>
        /// <returns>The page model</returns>
        public T GetStartpage<T>(Guid? siteId = null) where T : Models.PageBase {
            if (!siteId.HasValue) {
                var site = api.Sites.GetDefault();
                if (site != null)
                    siteId = site.Id;
            }

            var page = cache != null ? cache.Get<Page>($"Page_{siteId}") : null;

            if (page == null) {
                page = db.Pages
                    .AsNoTracking()
                    .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                    .Include(p => p.Fields)
                    .FirstOrDefault(p => p.SiteId == siteId && p.ParentId == null && p.SortOrder == 0);
                if (page != null) {
                    if (cache != null)
                        AddToCache(page);
                }
            }

            if (page != null) {
                return contentService.Transform<T>(page, api.PageTypes.GetById(page.PageTypeId), Process);
            }
            return null;
        }

        /// <summary>
        /// Gets the page model with the specified id.
        /// </summary>
        /// <param name="id">The unique id</param>
        /// <returns>The page model</returns>
        public Models.DynamicPage GetById(Guid id) {
            return GetById<Models.DynamicPage>(id);
        }

        /// <summary>
        /// Gets the page model with the specified id.
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <param name="id">The unique id</param>
        /// <returns>The page model</returns>
        public T GetById<T>(Guid id) where T : Models.PageBase {
            var page = cache != null ? cache.Get<Page>(id.ToString()) : null;

            if (page == null) {
                page = db.Pages
                    .AsNoTracking()
                    .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                    .Include(p => p.Fields)
                    .FirstOrDefault(p => p.Id == id);

                if (page != null) {
                    if (cache != null)
                        AddToCache(page);
                }
            }

            if (page != null)
                return contentService.Transform<T>(page, api.PageTypes.GetById(page.PageTypeId), Process);
            return null;
        }

        /// <summary>
        /// Gets the page model with the specified slug.
        /// </summary>
        /// <param name="slug">The unique slug</param>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The page model</returns>
        public Models.DynamicPage GetBySlug(string slug, Guid? siteId = null) {
            return GetBySlug<Models.DynamicPage>(slug, siteId);
        }

        /// <summary>
        /// Gets the page model with the specified slug.
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <param name="slug">The unique slug</param>
        /// <param name="siteId">The optional site id</param>
        /// <returns>The page model</returns>
        public T GetBySlug<T>(string slug, Guid? siteId = null) where T : Models.PageBase {
            if (!siteId.HasValue) {
                var site = api.Sites.GetDefault();
                if (site != null)
                    siteId = site.Id;
            }

            // See if we can get the page id for the slug from cache.
            var pageId = cache != null ? cache.Get<Guid?>($"PageId_{siteId}_{slug}") : (Guid?)null;

            if (pageId.HasValue) {
                // Load the page by id instead
                return GetById<T>(pageId.Value);
            } else {
                // No cache found, load from database
                var page = db.Pages
                    .AsNoTracking()
                    .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                    .Include(p => p.Fields)
                    .FirstOrDefault(p => p.SiteId == siteId && p.Slug == slug);

                if (page != null) {
                    if (cache != null)
                        AddToCache(page);
                    return contentService.Transform<T>(page, api.PageTypes.GetById(page.PageTypeId), Process);
                }                    
                return null;
            }
        }

        /// <summary>
        /// Gets the id for the page with the given slug.
        /// </summary>
        /// <param name="slug">The unique slug</param>
        /// <param name="siteId">The optional page id</param>
        /// <returns>The id</returns>
        public Guid? GetIdBySlug(string slug, Guid? siteId = null) {
            if (!siteId.HasValue) {
                var site = api.Sites.GetDefault();
                if (site != null)
                    siteId = site.Id;
            }

            // See if we can get the page id for the slug from cache.
            var pageId = cache != null ? cache.Get<Guid?>($"PageId_{siteId}_{slug}") : (Guid?)null;

            if (pageId.HasValue) {
                return pageId;
            } else {
                // No cache found, load from database
                var page = db.Pages
                    .AsNoTracking()
                    .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                    .Include(p => p.Fields)
                    .FirstOrDefault(p => p.SiteId == siteId && p.Slug == slug);

                if (page != null) {
                    if (cache != null)
                        AddToCache(page);
                    return page.Id;
                }                    
                return null;                
            }
        }

        /// <summary>
        /// Gets the hierachical sitemap structure.
        /// </summary>
        /// <param name="id">The optional site id</param>
        /// <param name="onlyPublished">If only published items should be included</param>
        /// <returns>The sitemap</returns>
        public Models.Sitemap GetSitemap(Guid? siteId = null, bool onlyPublished = true) {
            if (!siteId.HasValue) {
                var site = api.Sites.GetDefault();

                if (site != null)
                    siteId = site.Id;
            }

            if (siteId != null) {
                var sitemap = onlyPublished && cache != null ? cache.Get<Models.Sitemap>($"Sitemap_{siteId}") : null;

                if (sitemap == null) {
                    var pages = db.Pages
                        .AsNoTracking()
                        .Where(p => p.SiteId == siteId)
                        .OrderBy(p => p.ParentId)
                        .ThenBy(p => p.SortOrder)
                        .ToList();

                    var pageTypes = api.PageTypes.GetAll();

                    if (onlyPublished)
                        pages = pages.Where(p => p.Published.HasValue).ToList();
                    sitemap = Sort(pages, pageTypes);

                    if (onlyPublished && cache != null)
                        cache.Set($"Sitemap_{siteId}", sitemap);
                }
                return sitemap;
            }
            return null;
        }

        /// <summary>
        /// Moves the current page in the structure.
        /// </summary>
        /// <typeparam name="T">The model type</typeparam>
        /// <param name="model">The page to move</param>
        /// <param name="parentId">The new parent id</param>
        /// <param name="sortOrder">The new sort order</param>
        public void Move<T>(T model, Guid? parentId, int sortOrder) where T : Models.PageBase {
            IEnumerable<Page> oldSiblings = null;
            IEnumerable<Page> newSiblings = null;

            // Only get siblings if we need to invalidate from cache
            if (cache != null) {
                oldSiblings = db.Pages
                    .Where(p => p.ParentId == model.ParentId && p.Id != model.Id)
                    .ToList();
                newSiblings = db.Pages
                    .Where(p => p.ParentId == parentId)
                    .ToList();
            }

            // Remove the old position for the page
            MovePages(model.Id, model.SiteId, model.ParentId, model.SortOrder + 1, false);
            // Add room for the new position of the page
            MovePages(model.Id, model.SiteId, parentId, sortOrder, true);

            // Update the position of the current page
            var page = db.Pages
                .FirstOrDefault(p => p.Id == model.Id);
            page.ParentId = parentId;
            page.SortOrder = sortOrder;

            db.SaveChanges();

            // Remove all siblings from cache
            if (cache != null) {
                foreach (var sibling in oldSiblings)
                    RemoveFromCache(sibling);
                foreach (var sibling in newSiblings)
                    RemoveFromCache(sibling);
                InvalidateSitemap(model.SiteId);
            }
        }

        /// <summary>
        /// Saves the given page model
        /// </summary>
        /// <param name="model">The page model</param>
        public void Save<T>(T model) where T : Models.PageBase {
            var type = api.PageTypes.GetById(model.TypeId);

            if (type != null) {
                // Ensure that we have a slug
                if (string.IsNullOrWhiteSpace(model.Slug)) {
                    var prefix = "";

                    // Check if we should generate hierarchical slugs
                    using (var config = new Config(api)) {
                        if (config.HierarchicalPageSlugs && model.ParentId.HasValue) {
                            var parentSlug = db.Pages
                                .AsNoTracking()
                                .FirstOrDefault(p => p.Id == model.ParentId)?.Slug;

                            if (!string.IsNullOrWhiteSpace(parentSlug)) {
                                prefix = parentSlug + "/";
                            }
                        }
                        model.Slug = prefix + Utils.GenerateSlug(model.NavigationTitle != null ? model.NavigationTitle : model.Title);
                    }
                } else model.Slug = Utils.GenerateSlug(model.Slug);

                // Set content type
                model.ContentType = type.ContentTypeId;

                var page = db.Pages
                    .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                    .Include(p => p.Fields)
                    .FirstOrDefault(p => p.Id == model.Id);

                // Transform the model
                if (page == null) {
                    page = new Page() {
                        Id = model.Id != Guid.Empty ? model.Id : Guid.NewGuid(),
                        ParentId = model.ParentId,
                        SortOrder = model.SortOrder,
                        PageTypeId = model.TypeId,
                        Created = DateTime.Now,
                        LastModified = DateTime.Now
                    };
                    db.Pages.Add(page);
                    model.Id = page.Id;

                    // Make room for the new page
                    MovePages(page.Id, model.SiteId, model.ParentId, model.SortOrder, true);                    
                } else {
                    // Check if the page has been moved
                    if (page.ParentId != model.ParentId || page.SortOrder != model.SortOrder) {
                        // Remove the old position for the page
                        MovePages(page.Id, page.SiteId, page.ParentId, page.SortOrder + 1, false);
                        // Add room for the new position of the page
                        MovePages(page.Id, model.SiteId, model.ParentId, model.SortOrder, true);
                    }                    
                    page.LastModified = DateTime.Now;
                }
                page = contentService.Transform<T>(model, type, page);

                // Transform blocks
                IList<Extend.Block> blockModels = null;
                if (model is Models.IPage)
                    blockModels = ((Models.IPage)model).Blocks;
                else if (model is Models.IDynamicPage)
                    blockModels = ((Models.IDynamicPage)model).Blocks;

                if (blockModels != null && blockModels.Count > 0) {
                    var blocks = contentService.TransformBlocks(blockModels);
                    var current = blocks.Select(b => b.Id).ToArray();

                    // Delete removed blocks
                    var removed = page.Blocks
                        .Where(b => !current.Contains(b.BlockId) && !b.Block.IsReusable)
                        .Select(b => b.Block);
                    db.Blocks.RemoveRange(removed);

                    // Delete the old page blocks
                    page.Blocks.Clear();

                    // Now map the new block
                    for (var n = 0; n < blocks.Count; n++) {
                        var block = db.Blocks
                            .Include(b => b.Fields)
                            .FirstOrDefault(b => b.Id == blocks[n].Id);
                        if (block == null) {
                            block = new Block() {
                                Id = blocks[n].Id != Guid.Empty ? blocks[n].Id : Guid.NewGuid(),
                                Created = DateTime.Now
                            };
                            db.Blocks.Add(block);
                        }
                        block.CLRType = blocks[n].CLRType;
                        block.IsReusable = blocks[n].IsReusable;
                        block.Title = blocks[n].Title;
                        block.LastModified = DateTime.Now;

                        var currentFields = blocks[n].Fields.Select(f => f.FieldId).Distinct();
                        var removedFields = block.Fields.Where(f => !currentFields.Contains(f.FieldId));
                        db.BlockFields.RemoveRange(removedFields);

                        foreach (var newField in blocks[n].Fields) {
                            var field = block.Fields.FirstOrDefault(f => f.FieldId == newField.FieldId);
                            if (field == null) {
                                field = new BlockField() {
                                    Id = newField.Id != Guid.Empty ? newField.Id : Guid.NewGuid(),
                                    BlockId = block.Id,
                                    FieldId = newField.FieldId
                                };
                                db.BlockFields.Add(field);
                                block.Fields.Add(field);
                            }
                            field.SortOrder = newField.SortOrder;
                            field.CLRType = newField.CLRType;
                            field.Value = newField.Value;
                        }

                        // Create the page block
                        page.Blocks.Add(new PageBlock() {
                            Id = Guid.NewGuid(),
                            BlockId = block.Id,
                            Block = block,
                            PageId = page.Id,
                            SortOrder = n
                        });
                    }
                }
                db.SaveChanges();

                if (cache != null)
                    RemoveFromCache(page);
                InvalidateSitemap(model.SiteId);
            }
        }

        /// <summary>
        /// Deletes the model with the specified id.
        /// </summary>
        /// <param name="id">The unique id</param>
        public virtual void Delete(Guid id) {
            var model = db.Pages
                .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                .Include(p => p.Fields)
                .FirstOrDefault(p => p.Id == id);

            if (model != null) {
                // Remove all blocks that are not reusable
                foreach (var pageBlock in model.Blocks) {
                    if (!pageBlock.Block.IsReusable)
                        db.Blocks.Remove(pageBlock.Block);
                }
                // Remove the main page.
                db.Pages.Remove(model);

                // Move all remaining pages after this page in the site structure.
                MovePages(id, model.SiteId, model.ParentId, model.SortOrder + 1, false);

                db.SaveChanges();

                // Check if we have the page in cache, and if so remove it
                if (cache != null) {
                    var page = cache.Get<Page>(model.Id.ToString());
                    if (page != null)
                        RemoveFromCache(page);
                    InvalidateSitemap(model.SiteId);
                }   
            }
        }

        /// <summary>
        /// Deletes the given model.
        /// </summary>
        /// <param name="model">The model</param>
        public virtual void Delete<T>(T model) where T : Models.PageBase {
            Delete(model.Id);
        }

        /// <summary>
        /// Performs additional processing and loads related models.
        /// </summary>
        /// <param name="page">The source page</param>
        /// <param name="model">The targe model</param>
        private void Process<T>(Data.Page page, T model) where T : Models.PageBase {
            if (page.Blocks.Count > 0) {
                var blocks = page.Blocks
                    .OrderBy(b => b.SortOrder)
                    .Select(b => b.Block)
                    .ToList();

                if (model is Models.IPage)
                    ((Models.IPage)model).Blocks = contentService.TransformBlocks(blocks);
                else if (model is Models.IDynamicPage)
                    ((Models.IDynamicPage)model).Blocks = contentService.TransformBlocks(blocks);
            }
        }        

        /// <summary>
        /// Moves the pages around. This is done when a page is deleted or moved in the structure.
        /// </summary>
        /// <param name="pageId">The id of the page that is moved</param>
        /// <param name="siteId">The site id</param>
        /// <param name="parentId">The parent id</param>
        /// <param name="sortOrder">The sort order</param>
        /// <param name="increase">If sort order should be increase or decreased</param>
        private void MovePages(Guid pageId, Guid siteId, Guid? parentId, int sortOrder, bool increase) {
            var pages = db.Pages
                .Where(p => p.SiteId == siteId && p.ParentId == parentId && p.SortOrder >= sortOrder && p.Id != pageId)
                .ToList();

            foreach (var page in pages)
                page.SortOrder = increase ? page.SortOrder + 1 : page.SortOrder - 1;
        }

        /// <summary>
        /// Updates the LastModified date of the pages and
        /// removes it from the cache.
        /// </summary>
        /// <param name="pages">The id of the pages</param>
        internal void Touch(params Guid[] pages) {
            var models = db.Pages
                .Where(p => pages.Contains(p.Id))
                .ToArray();

            foreach (var page in models) {
                page.LastModified = DateTime.Now;
                db.SaveChanges();
                RemoveFromCache(page);
            }
        }

        /// <summary>
        /// Internal method for getting the data page by id.
        /// </summary>
        /// <param name="id">The unique id</param>
        /// <returns>The page</returns>
        internal Page GetPageById(Guid id) {
            var page = cache != null ? cache.Get<Page>(id.ToString()) : null;

            if (page == null) {
                page = db.Pages
                    .AsNoTracking()
                    .Include(p => p.Blocks).ThenInclude(b => b.Block).ThenInclude(b => b.Fields)
                    .Include(p => p.Fields)
                    .FirstOrDefault(p => p.Id == id);

                if (page != null) {
                    if (cache != null)
                        AddToCache(page);
                }
            }
            return page;
        }

        /// <summary>
        /// Sorts the items.
        /// </summary>
        /// <param name="pages">The full page list</param>
        /// <param name="parentId">The current parent id</param>
        /// <returns>The sitemap</returns>
        private Models.Sitemap Sort(IEnumerable<Page> pages, IEnumerable<Models.PageType> pageTypes, Guid? parentId = null, int level = 0) {
            var result = new Models.Sitemap();

            foreach (var page in pages.Where(p => p.ParentId == parentId).OrderBy(p => p.SortOrder)) {
                var item = App.Mapper.Map<Page, Models.SitemapItem>(page);

                item.Level = level;
                item.PageTypeName = pageTypes.First(t => t.Id == page.PageTypeId).Title;
                item.Items = Sort(pages, pageTypes, page.Id, level + 1);

                result.Add(item);
            }
            return result;
        }        

        /// <summary>
        /// Adds the given model to cache.
        /// </summary>
        /// <param name="page">The page</param>
        private void AddToCache(Page page) {
            cache.Set(page.Id.ToString(), page);
            cache.Set($"PageId_{page.SiteId}_{page.Slug}", page.Id);
            if (!page.ParentId.HasValue && page.SortOrder == 0)
                cache.Set($"Page_{page.SiteId}", page);
        }

        /// <summary>
        /// Removes the given model from cache.
        /// </summary>
        /// <param name="page">The page</param>
        private void RemoveFromCache(Page page) {
            cache.Remove(page.Id.ToString());
            cache.Remove($"PageId_{page.SiteId}_{page.Slug}");
            if (!page.ParentId.HasValue && page.SortOrder == 0)
                cache.Remove($"Page_{page.SiteId}");
        }

        /// <summary>
        /// Removes the specified public sitemap from
        /// the cache.
        /// </summary>
        /// <param name="id">The site id</param>
        private void InvalidateSitemap(Guid id) {
            if (cache != null)
                cache.Remove($"Sitemap_{id}");
        }        
    }
}
