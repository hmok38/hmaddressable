namespace HM
{
    public enum UpdateStatusCode
    {
        /// <summary>
        /// 不需要更新资源
        /// </summary>
        NO_UPDATES_NEEDED = 0,
        /// <summary>
        /// 正在检查资源列表
        /// </summary>
        CHECKING_RESOURCE_LIST,
        /// <summary>
        /// 检查资源列表时发生错误
        /// </summary>
        ERROR_CHECKING_RESOURCE_LIST,
        /// <summary>
        /// 正在核对需要更新的资源内容
        /// </summary>
        CHECKING_CONTENT_OF_UPDATING_RESOURCES,
        /// <summary>
        /// 更新资源列表时发生错误
        /// </summary>
        ERROR_UPDATING_RESOURCE_LIST,
        /// <summary>
        /// 正在获取资源文件总大小
        /// </summary>
        GETTING_TOTAL_SIZE_OF_RESOURCE_FILES,
        /// <summary>
        /// 获取资源文件大小时发生错误
        /// </summary>
        ERROR_GETTING_RESOURCE_FILE_SIZE,
        /// <summary>
        /// 检查到需要下载的资源大小为
        /// </summary>
        DETECTED_SIZE_OF_RESOURCES_TO_DOWNLOAD,
        /// <summary>
        /// 下载资源中
        /// </summary>
        DOWNLOADING_RESOURCES,
        /// <summary>
        /// 下载资源时发生错误
        /// </summary>
        ERROR_DOWNLOADING_RESOURCES ,
        /// <summary>
        /// 下载资源完成
        /// </summary>
        FINISHED_DOWNLOADING_RESOURCES,
    }
}