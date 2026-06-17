using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Planning.Requests;
using AgvDispatcher.Core.Contracts.Planning.Results;

namespace AgvDispatcher.Core.Contracts.Planning.Interfaces
{
    /// <summary>
    /// 路径规划模块契约接口。
    /// 负责基于给定的地图和动态约束条件进行路径搜索，不负责资源锁定和车辆控制。
    /// </summary>
    public interface IPathPlanner
    {
        /// <summary>
        /// 规划一条推荐路径。
        /// </summary>
        /// <param name="request">路径规划请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>路径规划结果</returns>
        Task<AgvResult<PathPlanResult>> PlanAsync(PathPlanRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 规划多条候选路径。
        /// </summary>
        /// <param name="request">路径规划请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含多条候选路径结果的列表</returns>
        Task<AgvResult<IReadOnlyList<PathPlanResult>>> PlanAlternativesAsync(PathPlanRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 判断起点到目标点是否可达。
        /// </summary>
        /// <param name="request">可达性检查请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>可达性检查结果</returns>
        Task<AgvResult<PathReachabilityResult>> CheckReachabilityAsync(PathReachabilityRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从候选点中寻找最近的可达点。
        /// </summary>
        /// <param name="request">最近节点查询请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>最近可达点查询结果</returns>
        Task<AgvResult<NearestNodeResult>> FindNearestReachableNodeAsync(NearestNodeRequest request, CancellationToken cancellationToken = default);
    }
}
