using System;
using System.Linq;
using System.Threading.Tasks;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.EntityViews;
using Sitecore.Commerce.Plugin.EntityVersions;
using Sitecore.Framework.Pipelines;

namespace Ajsuth.Foundation.EntityVersions.Engine.Pipelines.Blocks
{
    /// <summary>Defines the do action add entity block.</summary>
    /// <seealso cref="AsyncPipelineBlock{TInput, TOutput, TContext}" />
    [PipelineDisplayName(Sitecore.Commerce.Plugin.EntityVersions.EntityVersionsConstants.DoActionAddEntityVersion)]
    public class DoActionAddEntityVersionBlock : AsyncPipelineBlock<EntityView, EntityView, CommercePipelineExecutionContext>
    {
        private readonly CommerceCommander _commerceCommander;

        /// <summary>Initializes a new instance of the <see cref="DoActionAddEntityVersionBlock"/> class.</summary>
        /// <param name="commerceCommander">The <see cref="CommerceCommander"/> is a gateway object to resolving and executing other Commerce Commands and other control points.</param>
        public DoActionAddEntityVersionBlock(CommerceCommander commerceCommander)
        {
            _commerceCommander = commerceCommander;
        }

        /// <summary>Executes the pipeline block's code logic.</summary>
        /// <param name="arg">The pipeline argument.</param>
        /// <param name="context">The context.</param>
        /// <returns>The <see cref="EntityView"/>.</returns>
        public override async Task<EntityView> RunAsync(EntityView arg, CommercePipelineExecutionContext context)
        {
            if (string.IsNullOrEmpty(arg?.Action)
                || !arg.Action.Equals(context.GetPolicy<KnownEntityVersionsActionsPolicy>().AddEntityVersion, StringComparison.OrdinalIgnoreCase))
            {
                return arg;
            }

            var commerceEntity = context.CommerceContext.GetObjects<CommerceEntity>().FirstOrDefault(p => p.Id.Equals(arg.EntityId, StringComparison.OrdinalIgnoreCase));
            if (commerceEntity == null)
            {
                await context.CommerceContext.AddMessage(
                        context.GetPolicy<KnownResultCodes>().ValidationError,
                        "EntityNotFound",
                        new object[]
                        {
                            arg.EntityId
                        },
                        $"Entity {arg.EntityId} was not found.")
                    .ConfigureAwait(false);
                return arg;
            }

            var findArg = new FindEntityArgument(typeof(VersioningEntity), VersioningEntity.GetIdBasedOnEntityId(commerceEntity.Id));
            var versions = await _commerceCommander.Pipeline<IFindEntityPipeline>()
                .RunAsync(findArg, context)
                .ConfigureAwait(false);

            var latestVersion = (versions as VersioningEntity)?.LatestVersion(context.CommerceContext) ?? 1;
            var newVersion = latestVersion + 1;

            await _commerceCommander.ProcessWithTransaction(context.CommerceContext,
                   () => _commerceCommander.Pipeline<IAddEntityVersionPipeline>()
                   .RunAsync(new AddEntityVersionArgument(commerceEntity)
                   {
                       CurrentVersion = commerceEntity.EntityVersion,
                       NewVersion = newVersion
                   },
                   context.CommerceContext.PipelineContextOptions)).ConfigureAwait(false);

            context.CommerceContext.AddModel(
                new RedirectUrlModel($"/entityView/Master/{newVersion}/{arg.EntityId}")
            );

            return arg;
        }
    }
}
