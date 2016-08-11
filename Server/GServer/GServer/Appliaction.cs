﻿using System;
using XNet.Libs.Net;
using Proto;
using XNet.Libs.Utility;
using ServerUtility;
using MySql.Data.MySqlClient;
using System.Linq;
using org.vxwo.csharp.json;

namespace GServer
{
    public class Appliaction
    {
        
        int port;
        int ServicePort;
        string LoginHost;
        int LoginPort;
        string configRoot;
        string serverHostName;
        public int ServerID { set; get; }

        private string ConnectionString;

        internal Client GetClientByUserID(long userID)
        {
            Client res =null;
            this.ListenServer.CurrentConnectionManager.Each((obj) => {
                if ((long)obj.UserState == userID)
                {
                    res = obj;
                    return true;
                }
                return false;
            });
            return res;
        }

        public MySqlConnection Connection
        {
            get
            {
                return new MySqlConnection(this.ConnectionString);
            }
        }
        public static Appliaction Current { private set; get; }

        //玩家访问端口
        public SocketServer ListenServer { private set; get; }

        //游戏战斗服务器访问端口
        public SocketServer ServiceServer { private set; get; }

        public RequestClient Client { private set; get; }

        public volatile bool IsRunning;

        public Appliaction(JsonValue config)
        {
            RequestHandle.RegAssembly(this.GetType().Assembly);
            this.configRoot = config["ConfigPath"].AsString();
            this.port = config["ListenPort"].AsInt();
            this.ServicePort = config["ServicePort"].AsInt();
            this.LoginPort = config["LoginServerPort"].AsInt();
            this.LoginHost = config["LoginServerHost"].AsString();
            serverHostName = config["Host"].AsString();
            Current = this;
            this.ConnectionString = string.Format(
               "Data Source={0};Initial Catalog={1};Persist Security Info=True;User ID={2};Password={3}",
                config["DBHost"].AsString(), 
                config["DBName"].AsString(), 
                config["DBUser"].AsString(), 
                config["DBPwd"].AsString()
            );
            ServerID = config["ServerID"].AsInt();
           
        }

        public Client GetClientById(int index)
        {
            return ListenServer.CurrentConnectionManager.GetClientByID(index);
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
           
            ResourcesLoader.Singleton.LoadAllConfig(configRoot);


            var cm = new ConnectionManager();
            ListenServer = new SocketServer(cm, port);
            ListenServer.HandlerManager = new RequestHandle();
            ListenServer.Start();



            Client = new RequestClient(LoginHost, LoginPort);
            Client.RegAssembly(this.GetType().Assembly);
            Client.UseSendThreadUpdate = true;
            Client.OnConnectCompleted = (s, e) =>
            {
                if (e.Success)
                {
                    int currentPlayer = 0;
                    using (var db = new DataBaseContext.GameDb(Connection))
                    {
                        currentPlayer = db.TBGAmePlayer.Count();
                    }
                    var request = Client.CreateRequest<G2L_Reg, L2G_Reg>();
                    request.RequestMessage.ServerID = ServerID;
                    request.RequestMessage.Port = this.port;
                    request.RequestMessage.Host = serverHostName;
                    request.RequestMessage.MaxPlayer = 100000; //最大玩家数
                    request.RequestMessage.CurrentPlayer = currentPlayer;
                    request.RequestMessage.Version = ProtoTool.GetVersion();
                    request.OnCompleted = (success, r) =>
                    {
                        if (success && r.Code == ErrorCode.OK)
                        {
                            Debuger.Log("Server Reg Success!");
                        }

                    };
                    request.SendRequestSync();
                }
                else
                {
                    Debuger.Log("Can't connect LoginServer!");
                    Stop();

                }
            };
            Client.OnDisconnect = (s, e) => 
            {
                Debuger.Log("Can't connect LoginServer!");
                Stop();
            };
            Client.Connect();
        }

        public void Stop()
        {
            if (!IsRunning) 
                return;
            IsRunning = false;
            ListenServer.Stop();
            Client.Disconnect();
        }

        public void Tick()
        {
            
        }

       

    }
}
