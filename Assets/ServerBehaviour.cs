using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
//using UnityEditor.Compilation;
using System;

public class ServerBehaviour : MonoBehaviour
{
    public NetworkDriver m_Driver;
    private NativeList<NetworkConnection> m_Connections;
    public class Client 
    {
        public Vector3 pos;
        public Quaternion rot;
        public bool connected;
        public GameObject player;
        public DateTime lastPacket;
        public double latency;

        public void Update (Vector3 p, Quaternion r)
        {
            pos = p;
            rot = r;
            TimeSpan ts = DateTime.Now - lastPacket;
            latency = ts.TotalMilliseconds;
            lastPacket = DateTime.Now;
        }
    }
    Dictionary<int, Client> clients = new Dictionary<int, Client>();
    [TextArea(10, 100)]
    public string clientDebug;
    public GameObject playerPrefab;

    public float clientUpdateRate;
    float clientUpdateTimer = 0;

    void Start()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 9000;
        if (m_Driver.Bind(endpoint) != 0)
        {
            Debug.LogError("Failed to bind port 9000");
        } else
        {
            m_Driver.Listen();
        }

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
    }

    void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();
        //Cleanup connections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }
        //Accept new connections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default(NetworkConnection))
        {
            m_Connections.Add(c);
            Debug.Log("Accepted connection");
        }
        //Query events
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                continue;
            }

            NetworkEvent.Type cmd;
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    FixedString128 data = stream.ReadFixedString128();
                    OnReceiveData(data.ConvertToString(), i);
                } else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected");
                    m_Connections[i] = default(NetworkConnection);
                    Destroy(clients[i].player);
                    clients.Remove(i);
                }
            }

            if (clients.ContainsKey(i))
            {
                clients[i].player.transform.position = clients[i].pos;
                clients[i].player.transform.rotation = clients[i].rot;
            }

            
        }
        clientUpdateTimer += Time.deltaTime;
        if (clientUpdateTimer > clientUpdateRate && clients.Count > 1)
        {
            clientUpdateTimer = 0;
            
            for (int i = 0; i < m_Connections.Length; i++)
            {
                string s = "UPDATE|";
                foreach (var pair in clients)
                {
                    if (pair.Key != i)
                    {
                        s += pair.Key + ":" + pair.Value.pos.x + ":" + pair.Value.pos.y + ":" + pair.Value.pos.z;
                        s += ":" + pair.Value.rot.x + ":" + pair.Value.rot.y + ":" + pair.Value.rot.z + ":" + pair.Value.rot.w;
                        s += "_";
                    }
                }
                var writer = m_Driver.BeginSend(NetworkPipeline.Null, m_Connections[i]);
                FixedString128 responseData = new FixedString128(s);
                writer.WriteFixedString128(responseData);
                m_Driver.EndSend(writer);
            }   
        }
    }
    void OnReceiveData(string data, int client)
    {
        //Debug.Log("Received data: " + data);
        string[] a = data.Split('|');
        bool respond = false;
        string response = "";
        switch (a[0])
        {
            case "UPDATE":
                Vector3 pos = new Vector3(float.Parse(a[2]), float.Parse(a[3]), float.Parse(a[4]));
                Quaternion rot = new Quaternion(float.Parse(a[5]), float.Parse(a[6]), float.Parse(a[7]), float.Parse(a[8]));
                if (clients.ContainsKey(client))
                {
                    clients[client].Update(pos, rot);
                    //clients[client].pos = pos;
                    //clients[client].rot = rot;
                } else
                {
                    GameObject p = Instantiate(playerPrefab, pos, rot, this.transform);
                    clients.Add(client, new Client() { pos = pos, rot = rot, connected = true, player = p, lastPacket = DateTime.Now });
                }
                string d = "";
                foreach (var pair in clients)
                {
                    d += pair.Key + " : " + pair.Value.latency.ToString() + "\n";
                }
                clientDebug = d;
                break;
            case "ID":
                respond = true;
                response = "ID|" + client;
                break;
            case "PING":
                respond = true;
                response = "PONG| ";
                break;
        }
        if (respond)
        {
            var writer = m_Driver.BeginSend(NetworkPipeline.Null, m_Connections[client]);
            FixedString128 responseData = new FixedString128(response);
            writer.WriteFixedString128(responseData);
            m_Driver.EndSend(writer);
        }
    }
}
