using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMassage;

public class NetworkClient : MonoBehaviour
{
    public static NetworkClient instance { get; private set; }

    public string IP;
    public ushort Port;
    public GameObject cube;

    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;

    public string myID { get; private set; }
    private Dictionary<string, GameObject> players;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }

        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
       
        var endpoint = NetworkEndPoint.Parse(IP, Port);
        m_Connection = m_Driver.Connect(endpoint);

        players = new Dictionary<string, GameObject>();
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }

 
    private void OnData(DataStreamReader stream)
    {
        NativeArray<byte> message = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(message);
        string returnData = Encoding.ASCII.GetString(message.ToArray());

        NetworkHeader header = new NetworkHeader();
        header = JsonUtility.FromJson<NetworkHeader>(returnData);

        switch (header.cmd)
        {
            case Commands.NEW_CLIENT:
                {
                    Debug.Log("New client");
                    NewPlayer np = JsonUtility.FromJson<NewPlayer>(returnData);
                    Debug.Log(np.player.ToString());
                    SpawnPlayers(np.player);
                    break;
                }
            case Commands.UPDATE:
                {
                    UpdatedPlayer up = JsonUtility.FromJson<UpdatedPlayer>(returnData);
                    UpdatePlayers(up.update);
                    break;
                }
            case Commands.CLIENT_DROPPED:
                {
                    DisconnectedPlayer dp = JsonUtility.FromJson<DisconnectedPlayer>(returnData);
                    DestroyPlayers(dp.disconnect);
                    Debug.Log("Client dropped");
                    break;
                }
            case Commands.CLIENT_LIST:
                {
                    ConnectedPlayer cp = JsonUtility.FromJson<ConnectedPlayer>(returnData);
                    SpawnPlayers(cp.connect);
                    Debug.Log("Client list");
                    break;
                }
            case Commands.OWN_ID:
                {
                    NewPlayer p = JsonUtility.FromJson<NewPlayer>(returnData);
                    myID = p.player.id;
                    SpawnPlayers(p.player);
                    break;
                }
            default:
                Debug.Log("Error");
                break;
        }
       
    }


    private void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
         return;
       

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        
        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("Connected to the server.");
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
               
                Debug.Log("Client disconnected");
                m_Connection = default(NetworkConnection);
            }
        }
    }

    private void SpawnPlayers(Player p)
    {
        if (players.ContainsKey(p.id))
           return;
       
        Debug.Log(p.ToString());
        GameObject temp = Instantiate(cube, p.position, p.rotation);
        temp.GetComponent<NetworkCharacter>().SetNetworkID(p.id);
        temp.GetComponent<NetworkCharacter>().SetControllable(p.id == myID);
        temp.GetComponent<Renderer>().material.color = new Color(p.color.R, p.color.G, p.color.B, 1.0f);
        players.Add(p.id, temp);
    }
    private void SpawnPlayers(Player[] p)
    {
        foreach (Player player in p)
        {
            SpawnPlayers(player);
        }
    }
    private void UpdatePlayers(Player[] p)
    {
        foreach (Player player in p)
        {
            if (players.ContainsKey(player.id))
            {
                players[player.id].transform.position = player.position;
                players[player.id].transform.rotation = player.rotation;
            }
        }
    }
    private void DestroyPlayers(Player[] p)
    {
        foreach (Player player in p)
        {
            if (players.ContainsKey(player.id))
            {
                Destroy(players[player.id]);
                players.Remove(player.id);
            }
        }
    }

    private void SendData(object data)
    {
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> sendBytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(JsonUtility.ToJson(data)), Allocator.Temp);
        writer.WriteBytes(sendBytes);
        m_Driver.EndSend(writer);
    }
    public void SendInput(Vector3 input)
    {
        PlayerInput playerInput = new PlayerInput();
        playerInput.input = input;
        SendData(playerInput);
    }
}