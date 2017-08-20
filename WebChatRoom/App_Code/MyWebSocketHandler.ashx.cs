using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;

namespace WebChatRoom.App_Code
{
    /// <summary>
    /// MyWebSocketHandler 的摘要说明
    /// </summary>
    public class MyWebSocketHandler : IHttpHandler,
        System.Web.SessionState.IRequiresSessionState  //确保Session值读写正常
    {
        private string _UserName = string.Empty;
        public void ProcessRequest(HttpContext context)
        {
            if (context.IsWebSocketRequest)
            {
                this._UserName = context.Request["UserName"];
                context.AcceptWebSocketRequest(ProcessChat);
            }
            else
            {
                context.Response.ContentType = "text/plain";
                context.Response.Write("请开启WebSocket Client连接");
            }
        }

        private async Task ProcessChat(AspNetWebSocketContext context)
        {
            WebSocket socket = context.WebSocket;
            CancellationToken cancellationToken = new CancellationToken();
            ChatManager.AddUser(_UserName, socket);

            await ChatManager.SendMessage(cancellationToken, $"{DateTime.Now.ToString("yyyyMMdd-HHmmss:fff")} {this._UserName} 进入聊天室");


            while (socket.State == WebSocketState.Open)
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[2048]);
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) //如果输入帧为取消帧，发送close命令
                {
                    //放在前面移除和发消息，  因为直接关浏览器会导致CloseAsync异常
                    ChatManager.RemoveUser(_UserName);
                    await ChatManager.SendMessage(cancellationToken, $"{DateTime.Now.ToString("yyyyMMdd-HHmmss:fff")} {this._UserName} 离开聊天室");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, cancellationToken);
                }
                else//获取字符串
                {
                    string userMsg = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                    string content = $"{DateTime.Now.ToString("yyyyMMdd-HHmmss:fff")} {this._UserName} 发送了：{userMsg}";
                    await ChatManager.SendMessage(cancellationToken, content);
                }
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }

    public class ChatManager
    {
        private static ConcurrentDictionary<string, WebSocket> _UserDictionary = new ConcurrentDictionary<string, WebSocket>();
        public static void AddUser(string name, WebSocket socket)
        {
            //    _UserDictionary.GetOrAdd(name, socket);
            _UserDictionary[name] = socket;
        }

        public static void RemoveUser(string name)
        {
            WebSocket socket;
            _UserDictionary.TryRemove(name, out socket);
        }

        public static async Task SendMessage(CancellationToken cancellationToken, string content)
        {
            //   ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[2048]); 此代码无效
            ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(content));
            foreach (var socket in _UserDictionary.Select(d => d.Value))
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
            }
        }
    }
}