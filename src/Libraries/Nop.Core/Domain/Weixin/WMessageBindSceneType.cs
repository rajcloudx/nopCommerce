﻿namespace Nop.Core.Domain.Weixin
{
    /// <summary>
    /// Represents a MessageBindSceneType
    /// </summary>
    public enum WMessageBindSceneType : byte
    {
        //永久二维码消息
        QrcodeLimit = 1,

        //临时二维码消息
        QrcodeTemp = 2,

        //图片消息
        Image = 3
    }
}
