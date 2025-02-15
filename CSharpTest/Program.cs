﻿using CSharpTest;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Tmds.DBus;

public struct SecretStruct
{
  public ObjectPath ObjectPath { get; set; }
  public byte[] ByteArray1 { get; set; }
  public byte[] ByteArray2 { get; set; }
  public string String { get; set; }

  public static implicit operator (ObjectPath, byte[], byte[], string)(SecretStruct secretStruct)
  {
    return (secretStruct.ObjectPath, secretStruct.ByteArray1, secretStruct.ByteArray2, secretStruct.String);
  }
}

public class SecretStorage
{
  public const string DEFAULT_COLLECTION = "/org/freedesktop/secrets/aliases/default";
  public Connection Connection { get; set; }
  public IService ServiceProxy { get; set; }
  public ICollection CollectionProxy { get; set; }
  public ObjectPath Session { get; set; }

  public async Task Init(string collection = DEFAULT_COLLECTION)
  {
    Connection = new Connection(Address.Session);
    await Connection.ConnectAsync();
    Console.WriteLine("Connected !");

    // Create proxies to call methods
    ServiceProxy = Connection.CreateProxy<IService>("org.freedesktop.secrets", "/org/freedesktop/secrets");
    CollectionProxy = Connection.CreateProxy<ICollection>("org.freedesktop.secrets", collection);

    await CreateSession();
    await UnlockSession();
  }

  private async Task CreateSession()
  {
    Session = (await ServiceProxy.OpenSessionAsync("plain", "my-session")).result;
    Console.WriteLine($"Created session: {Session}");
  }

  private async Task UnlockSession()
  {
    var (unlocked, unlockPrompt) = await ServiceProxy.UnlockAsync([DEFAULT_COLLECTION]);
    if (unlockPrompt == "/")
    {
      Console.WriteLine("No need to prompt for unlocking");
    }
    else
    {
      Console.WriteLine("Unlocking failed. Prompting for unlocking");
      var promptProxy = Connection.CreateProxy<IPrompt>("org.freedesktop.secrets", unlockPrompt);
      var watch = await promptProxy.WatchCompletedAsync((result) => { Console.WriteLine($"Prompt completed: {result}"); });
      await promptProxy.PromptAsync("");
    }

    Console.WriteLine($"Unlocked [{string.Join(", ", unlocked)}] ({unlocked.Length}). Prompt: {unlockPrompt}");
  }

  public async Task CreateItem()
  {
    var secret = new SecretStruct
    {
      ObjectPath = this.Session,
      ByteArray1 = Encoding.UTF8.GetBytes(""),
      ByteArray2 = Encoding.UTF8.GetBytes("HELLO-THIS-IS-SECRET-2"),
      String = "text/plain"
    };

    var (createdItem, prompt) = await this.CollectionProxy.CreateItemAsync(
      new Dictionary<string, object>
      {
        ["application"] = "MyApp/my-app",
        ["service"] = "MyApp",
        ["org.freedesktop.Secret.Item.Label"] = "My secret name"
      },
      secret, false
    );
    Console.WriteLine($"Secret created (createdItem: {createdItem}, prompt: {prompt})");
  }

  public async Task ListItems()
  {
    var props = await this.CollectionProxy.GetAllAsync();
    foreach (var item in props.Items)
    {
      var itemProxy = this.Connection.CreateProxy<IItem>("org.freedesktop.secrets", item);
      var itemProps = await itemProxy.GetAllAsync();
      Console.WriteLine($"Item: {item}, Type: {itemProps.Type}, Label: {itemProps.Label}");
      Console.WriteLine($"Attributes ({itemProps.Attributes.Count}): {string.Join(", ", itemProps.Attributes)}");
      Console.WriteLine();
    }
  }
  
  // Doesn't work well, only returns some items
  public async Task ListItemsLegacy()
  {
    var res = await this.CollectionProxy.SearchItemsAsync(new Dictionary<string, string>());
    Console.WriteLine($"Listing {res.Length} items: {string.Join(", ", res)}");
    foreach (var item in res)
    {
      // Get item
      var itemProxy = this.Connection.CreateProxy<IItem>("org.freedesktop.secrets", item);
      var props = await itemProxy.GetAllAsync();
      Console.WriteLine("----------");
      Console.WriteLine($"Props: {props.Created}, {props.Modified}, {props.Label}, {props.Type}");
      Console.WriteLine($"Attributes: {string.Join(", ", props.Attributes)}");
    }
  }
}

class Program
{
  static async Task Main()
  {
    var secretStorage = new SecretStorage();
    await secretStorage.Init();
    await secretStorage.CreateItem();
    await secretStorage.ListItems();
  }
}