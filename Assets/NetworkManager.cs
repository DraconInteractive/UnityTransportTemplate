using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviour
{
    public enum Mode
    {
        Init,
        Server,
        Client,
        ClientServer
    }

    public Mode mode;

    [Header("Prefabs")]
    public GameObject clientPrefab;
    public GameObject serverPrefab;

    [Header("UI")]
    public GameObject initWindow;
    public GameObject debugWindow;
    public Button clientStart, serverStart, clientServerStart;
    public Text debugText;
    public InputField ipInput;

    ServerBehaviour server;
    ClientBehaviour client;

    string myIP;

    private void Awake()
    {
        clientStart.onClick.AddListener(() => StartClient());
        serverStart.onClick.AddListener(() => StartServer());
        clientServerStart.onClick.AddListener(() => StartServerClient());
        mode = Mode.Init;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (mode == Mode.Server)
        {
            debugText.text = myIP + "\n";
            debugText.text += server.clientDebug;
        }
    }

    void EndInit ()
    {
        switch (mode)
        {
            case Mode.Client:
                transform.GetChild(0).gameObject.SetActive(false);
                break;
            case Mode.Server:
                initWindow.SetActive(false);
                debugWindow.SetActive(true);
                break;
            case Mode.ClientServer:
                initWindow.SetActive(false);
                debugWindow.SetActive(true);
                break;
        }
    }

    void StartClient ()
    {
        mode = Mode.Client;
        EndInit();
        
        client = Instantiate(clientPrefab, Vector3.zero, Quaternion.identity, this.transform).GetComponent<ClientBehaviour>();
        client.Create(9000, IPAddress.Parse(ipInput.text));
    }
    
    void StartServer ()
    {
        mode = Mode.Server;
        EndInit();
       
        server = Instantiate(serverPrefab, this.transform).GetComponent<ServerBehaviour>();
        using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endpoint = socket.LocalEndPoint as IPEndPoint;
            myIP = endpoint.Address.ToString();
            debugText.text = myIP;
        }
    }

    void StartServerClient ()
    {
        mode = Mode.ClientServer;
        EndInit();
        client = Instantiate(clientPrefab, this.transform).GetComponent<ClientBehaviour>();
        server = Instantiate(serverPrefab, this.transform).GetComponent<ServerBehaviour>();
    }
}
