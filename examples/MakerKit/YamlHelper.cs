using System;
using System.Collections.Generic;
using System.IO;

namespace MakerKit
{
    public class RegisterBankDefinition
    {
        public byte I2CAddress { get; set; }
        public string Name { get; set; }
        public string[] Registers { get; set; }
    }

    public static class YamlHelper
    {
        public static void WriteYaml(List<RegisterBankDefinition> allRegisters, string filePath)
        {
            TextWriter writer;
            try
            {
                writer = File.CreateText(filePath);
            }
            catch
            {
                throw new Exception("Error while trying to create Yaml file");
            }
            var serializer = new YamlDotNet.Serialization.Serializer();
            serializer.Serialize(writer, allRegisters);
            writer.Close();

        }

        public static List<RegisterBankDefinition> ReadYaml(string filePath)
        {
            //check if file exists, if not: make default file
            if (!File.Exists(filePath))
            {
                RegisterBankDefinition rd = new RegisterBankDefinition();
                rd.Name = "userBank";
                rd.I2CAddress = 22;
                rd.Registers = new string[] { "reg0", "reg1" };

                List<RegisterBankDefinition> allRegisters = new List<RegisterBankDefinition>();
                allRegisters.Add(rd);
                WriteYaml(allRegisters, filePath);
            }

            //file is sure to exist: read and parse
            StreamReader reader = File.OpenText(filePath);
            var deserializer = new YamlDotNet.Serialization.Deserializer();
            List<RegisterBankDefinition> registerBanks = deserializer.Deserialize<List<RegisterBankDefinition>>(reader);

            return registerBanks;
        }
    }
}

