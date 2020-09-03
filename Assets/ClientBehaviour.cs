using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine.UI;
using System.Net;
using System;

public class ClientBehaviour : MonoBehaviour
{
    public int ID;
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public bool Created;
    public bool Connected;
    public float positionRefreshRate;
    public float pingRate;
    float updateTimer = 0;
    float pingTimer = 0;
    public Button disconnectButton;
    public GameObject clientPrefab;
    Dictionary<int, ServerBehaviour.Client> Clients = new Dictionary<int, ServerBehaviour.Client>();
    public float m_smoothSpeed, r_smoothSpeed;
    public void Create(ushort port, IPAddress ep)
    {
        ID = UnityEngine.Random.Range(0, 1000);
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);

        NativeArray<byte> nativeArrayAddress;
        nativeArrayAddress = new NativeArray<byte>(ep.GetAddressBytes().Length, Allocator.Temp);
        nativeArrayAddress.CopyFrom(ep.GetAddressBytes());
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.SetRawAddressBytes(nativeArrayAddress);
        endpoint.Port = port;
        m_Connection = m_Driver.Connect(endpoint);

        disconnectButton.onClick.AddListener(() => Disconnect());
        Clients = new Dictionary<int, ServerBehaviour.Client>();
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();
        Created = m_Connection.IsCreated;
        if (!m_Connection.IsCreated)
        {
            if (!Created)
                //Debug.LogError("Connection Failed");
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("Client Connected");

                var writer = m_Driver.BeginSend(m_Connection);
                writer.WriteFixedString128(new FixedString128(IDRequestPacket()));
                m_Driver.EndSend(writer);

                Connected = true;
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                FixedString128 data = stream.ReadFixedString128();
                OnReceiveData(data.ConvertToString());
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Disconnected from server");
                m_Connection = default(NetworkConnection);
                Connected = false;
            }
        }
        if (Connected)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer > positionRefreshRate)
            {
                updateTimer = 0;
                var writer = m_Driver.BeginSend(m_Connection);
                writer.WriteFixedString128(new FixedString128(UpdatePacket()));
                m_Driver.EndSend(writer);
            }

            pingTimer += Time.deltaTime;
            if (pingTimer > pingRate)
            {
                pingTimer = 0;
                var writer = m_Driver.BeginSend(m_Connection);
                writer.WriteFixedString128(new FixedString128(PingPacket()));
                m_Driver.EndSend(writer);
            }

            if (Clients.Count > 0)
            {
                foreach (var pair in Clients)
                {
                    pair.Value.player.transform.position = Vector3.MoveTowards(pair.Value.player.transform.position, pair.Value.pos, m_smoothSpeed * Time.deltaTime);
                    pair.Value.player.transform.rotation = Quaternion.RotateTowards(pair.Value.player.transform.rotation, pair.Value.rot, r_smoothSpeed * Time.deltaTime);
                }
            }
        }
    }

    private void OnGUI()
    {
        string debug;
        debug = $"Connected: {Connected}\n";
        debug += $"Clients: {Clients.Count}\n";
        foreach (int i in Clients.Keys)
        {
            //debug += $"{i} >> {Clients[i].pos.ToString()} >> {Clients[i].rot.ToString()} >> {Clients[i].latency.ToString()}\n";
            debug += $"{i} >> {Clients[i].latency.ToString()}\n";
        }
        GUI.Box(new Rect(new Vector2(200, 200), new Vector2(450, 300)), debug);
    }
    [ContextMenu("Disconnect")]
    void Disconnect ()
    {
        Created = true;
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
#if UNITY_EDITOR
#else
        Application.Quit();
#endif

    }
    void OnReceiveData (string data)
    {
        Debug.Log($"Value <{data}> received from server");
        string[] a = data.Split('|');
        switch (a[0])
        {
            case "ID":
                if (int.TryParse(a[1], out int idResult))
                {
                    ID = idResult;
                }
                break;
            case "PONG":
                break;
            case "UPDATE":
                string[] otherClients = a[1].Split('_');
                foreach (string _oc in otherClients)
                {
                    string[] clientDetails = _oc.Split(':');
                    int oc_id = int.Parse(clientDetails[0]);
                    Vector3 oc_pos = new Vector3(float.Parse(clientDetails[1]), float.Parse(clientDetails[2]), float.Parse(clientDetails[3]));
                    Quaternion oc_rot = new Quaternion(float.Parse(clientDetails[4]), float.Parse(clientDetails[5]), float.Parse(clientDetails[6]), float.Parse(clientDetails[7]));
                    if (Clients.ContainsKey(oc_id))
                    {
                        //Clients[oc_id].pos = oc_pos;
                        //Clients[oc_id].rot = oc_rot;
                        Clients[oc_id].Update(oc_pos, oc_rot);
                    } else
                    {
                        GameObject oc_player = Instantiate(clientPrefab, oc_pos, oc_rot);
                        Clients.Add(oc_id, new ServerBehaviour.Client() { pos = oc_pos, rot = oc_rot, player = oc_player, lastPacket = DateTime.Now });
                    }
                }
                break;
        }
    }

    string UpdatePacket ()
    {
        return "UPDATE" + "|" + ID.ToString() + "|" + transform.position.x + "|" + transform.position.y + "|" + transform.position.z + "|" + transform.rotation.x + "|" + transform.rotation.y + "|" + transform.rotation.z + "|" + transform.rotation.w;
    }
    string IDRequestPacket ()
    {
        return "ID| ";
    }
    string PingPacket ()
    {
        return "PING| ";
    }
}
