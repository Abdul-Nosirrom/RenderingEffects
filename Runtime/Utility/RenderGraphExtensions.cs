using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering.Utility
{
    public static class RenderGraphExtensions
    {
        public static RendererListHandle CreateRendererList(this RenderGraph renderGraph, List<ShaderTagId> shaderTagIds, ContextContainer frameData, RenderQueueRange? queue = null)
        {
            var renderData = frameData.Get<UniversalRenderingData>();
            var camData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(shaderTagIds, renderData, camData, lightData, SortingCriteria.CommonTransparent);//camData.defaultOpaqueSortFlags);
            FilteringSettings filterSettings = FilteringSettings.defaultValue;
            filterSettings.renderQueueRange = queue ?? RenderQueueRange.transparent;
            RendererListParams renderingParams =
                new RendererListParams(renderData.cullResults, drawSettings, filterSettings);
            return renderGraph.CreateRendererList(renderingParams);
        }
    }
}