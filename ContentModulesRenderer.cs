using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Codegarden2024.Utilities.ContentModulesRenderer
{
    [HtmlTargetElement("contentModulesRenderer", TagStructure = TagStructure.WithoutEndTag)]
    public class ContentModulesRenderer : TagHelper
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IViewComponentHelper _viewComponentHelper;

        [Required]
        public string Name { get; set; }

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public ContentModulesRenderer(IUmbracoContextAccessor umbracoContextAccessor, IViewComponentHelper viewComponentHelper)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _viewComponentHelper = viewComponentHelper;
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = string.Empty;

            _ = _umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext umbracoContext);

            IEnumerable<IPublishedContent> children = umbracoContext?.PublishedRequest?.PublishedContent?.Children;

            IPublishedContent contentFolder = children.FirstOrDefault(x => x.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase));

            if (contentFolder?.Children is null || !contentFolder.Children.Any())
            {
                await base.ProcessAsync(context, output);
                return;
            }

            ((IViewContextAware)_viewComponentHelper).Contextualize(ViewContext);

            List<Task> renderTasks = [];
            ConcurrentDictionary<Guid, IHtmlContent> keyContentCollection = new();

            foreach (IPublishedContent module in contentFolder.Children)
            {
                Task renderTask = new(async () => keyContentCollection.TryAdd(module.Key, await _viewComponentHelper.InvokeAsync(FirstCharToUpper(module.ContentType.Alias), new { moduleKey = module.Key })));

                renderTask.Start();
                renderTasks.Add(renderTask);
            }

            await Task.WhenAll(renderTasks);

            IEnumerable<Guid> keysInOrder = contentFolder.Children.Select(x => x.Key);

            foreach (Guid key in keysInOrder)
            {
                IHtmlContent content = keyContentCollection.GetValueOrDefault(key);

                if (content is not null)
                {
                    output.Content.AppendHtml(content);
                }
            }
        }

        private string FirstCharToUpper(string alias)
        {
            return string.Concat(alias[0].ToString().ToUpper(), alias.AsSpan(1));
        }
    }
}
