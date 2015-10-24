using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChipSharp
{
    public class Chip
    {
        ushort opcode = 0; //This is the place where current opcode will be stored. 16bit/byte
        ushort I = 0; //This is the Index register. 16bit/byte
        ushort pc = 512; //This is the program counter. 16bit/byte

        byte[] memory = new byte[4096]; //This memory space is created because chip8 has 4k memory; Each is of course 8bit / 1byte
        byte[] v = new byte[16];

        public bool isInputExecuted = false;
        public byte lastInput;
        //Syteam Memory Map 
        //0x000-0x1FF - Chip 8 interpreter (contains font set in emu)
        //0x050 - 0x0A0 - Used for the built in 4x5 pixel font set(0 - F)
        //0x200 - 0xFFF - Program ROM and work RAM

        //Graphics
        //Chip 8 has 64x32 black and white screen so we need a matrix to store this info
        public bool[,] gfx = new bool[64, 32];

        //timers and counter
        byte delay_timer = 0;
        byte sound_timer = 0;

        //subroutine stack
        //when jumping to subroutines.
        Stack<ushort> routeStack = new Stack<ushort>();

        public byte[] key = new byte[16];

        public void initialize(string filename)
        {
            BinaryReader br = new BinaryReader(File.Open(filename, FileMode.Open));
            for (int i = 0; i < br.BaseStream.Length; i++)
            {
                byte code = br.ReadByte();
                memory[512 + i] = code;
            }

            //add fonts to the start of the memory

            byte[] fonts = new byte[]
            {
                0xF0,0x90,0x90,0x90,0xF0, //0
                0x20,0x60,0x20,0x20,0x70, //1
                0xF0,0x10,0xF0,0x80,0xF0, //2
                0xF0,0x10,0xF0,0x10,0xF0, //3
                0x90,0x90,0xF0,0x10,0x10, //4
                0xF0,0x80,0xF0,0x10,0xF0, //5
                0xF0,0x80,0xF0,0x90,0xF0, //6
                0xF0,0x10,0x20,0x40,0x40, //7
                0xF0,0x90,0xF0,0x90,0xF0, //8
                0xF0,0x90,0xF0,0x10,0xF0, //9
                0xF0,0x90,0xF0,0x90,0x90, //A
                0xE0,0x90,0xE0,0x90,0xE0, //B
                0xF0,0x80,0x80,0x80,0xF0, //C
                0xE0,0x90,0x90,0x90,0xE0, //D
                0xF0,0x80,0xF0,0x80,0xF0, //E
                0xF0,0x80,0xF0,0x80,0x80
            };

            for (int i = 0; i < fonts.Length; i++)
            {
                memory[i] = fonts[i];
            }
        }

        public void tick()
        {
            ushort p1, p2, p3;

            //10101010
            //01010101

            // Fetch opcode
            opcode = memory[pc];
            opcode = (ushort)((ushort)(opcode << 8) | memory[pc + 1]);

            p1 = (byte)(memory[pc] & 0x0F);
            p2 = (byte)(memory[pc + 1] >> 4);
            p3 = (byte)(memory[pc + 1] & 0x0F);
            // Decode opcode
            switch (opcode & 0xF000)
            {
                case 0x0000:
                    if (p1 == 0x0)
                    {
                        if (p3 == 0x0)
                        {
                            gfx = new bool[64, 32];
                        }
                        if (p3 == 0xE)
                        {
                            pc = (ushort)(routeStack.Pop());
                        }
                    }
                    else
                    {
                        Console.WriteLine("RCA 1802");
                    }

                    break;
                case 0x1000: // 1NNN: Jump to the address NNN
                    pc = (ushort)((opcode & 0x0FFF) - 2);
                    break;
                case 0x2000: // 1NNN: Jump to the address NNN
                    routeStack.Push(pc);
                    pc = (ushort)((opcode & 0x0FFF) - 2);
                    break;
                case 0x3000:
                    if (v[p1] == memory[pc + 1])
                        pc += 2;
                    break;
                case 0x4000:
                    if (v[p1] != memory[pc + 1])
                        pc += 2;
                    break;
                case 0x5000:
                    if (v[p1] == v[p2])
                        pc += 2;
                    break;
                case 0x6000:
                    v[p1] = memory[pc + 1];
                    break;
                case 0x7000:
                    v[p1] += memory[pc + 1];
                    break;
                case 0x8000:
                    switch (p3)
                    {
                        case 0x0: v[p1] = v[p2]; break;
                        case 0x1: v[p1] |= v[p2]; break;
                        case 0x2: v[p1] &= v[p2]; break;
                        case 0x3: v[p1] ^= v[p2]; break;
                        case 0x4:
                            if ((v[p1] + v[p2]) > 255)
                                v[15] = 1;
                            else
                                v[15] = 0;
                            v[p1] += v[p2];
                            break;
                        case 0x5:
                            if ((v[p1] - v[p2]) < 0)
                                v[15] = 0;
                            else
                                v[15] = 1;
                            v[p1] -= v[p2];
                            break;
                        case 0x6:
                            v[15] = (byte)(v[p1] & 0x1);
                            v[p1] >>= 1;
                            break;
                        case 0x7:
                            if ((v[p2] - v[p1]) < 0)
                                v[15] = 0;
                            else
                                v[15] = 1;
                            v[p1] = (byte)(v[p2] - v[p1]);
                            break;
                        case 0xe:
                            v[15] = (byte)((v[p1] & 0x8) == 0 ? 0 : 1);
                            v[p1] <<= 1;
                            break;
                    }
                    break;
                case 0x9000:
                    if (v[p1] != v[p2])
                    {
                        pc += 2;
                    }
                    break;
                case 0xA000: // ANNN: Sets I to the address NNN
                    I = (ushort)(opcode & 0x0FFF);
                    break;
                case 0xB000:
                    pc = (ushort)((opcode & 0x0FFF) + v[0] - 2);
                    break;
                case 0xC000:
                    Random rnd = new Random();
                    v[p1] = (byte)(rnd.Next(255) & memory[pc + 1]);
                    break;
                case 0xD000:
                    v[15] = 0;

                    for (int height = 0; height < p3; height++)
                    {
                        byte spritePart = memory[I + height];

                        for (int width = 0; width < 8; width++)
                        {
                            if ((spritePart & (0x80 >> width)) != 0)
                            {
                                ushort _x;
                                ushort _y;

                                _x = (ushort)((v[p1] + width) % 64);
                                _y = (ushort)((v[p2] + height) % 32);

                                if (gfx[_x,_y] == true)
                                {
                                    v[15] = 1;
                                }

                                gfx[_x,_y] ^= true;
                            }
                        }
                    }
                    break;
                case 0xE000:
                    if (p3 == 0xE)
                    {
                        if (key[v[p1]] == 1)
                        {
                            pc += 2;
                        }
                    }
                    else
                    {
                        if (key[v[p1]] != 1)
                        {
                            pc += 2;
                        }
                    }
                    break;
                case 0xF000:
                    switch (p3)
                    {
                        case 0x7:
                            v[p1] = delay_timer;
                            break;
                        case 0xA:
                            if (!isInputExecuted)
                            {
                                pc -= 2;
                                v[p3] = lastInput;
                            }
                            isInputExecuted = false;

                            break;
                        case 0x5:
                            switch (p2)
                            {
                                case 0x1:
                                    delay_timer = v[p1];
                                    break;
                                case 0x5:
                                    for (int i = 0; i <= p1; i++)
                                    {
                                        memory[I + i] = v[i];
                                    }
                                    break;
                                case 0x6:
                                    for (int i = 0; i <= p1; i++)
                                    {
                                        v[i] = memory[I + i];
                                    }
                                    break;
                            }
                            break;
                        case 0x8:
                            sound_timer = v[p1];
                            break;
                        case 0xE:
                            I += v[p1];
                            break;
                        case 0x9:
                            I = (ushort)(v[p1]*5);
                            break;
                        case 0x3:
                            decimal number = memory[v[p1]];
                            memory[I] = (byte)(number / 100);
                            memory[I+1] = (byte)((number % 100)/10);
                            memory[I+2] = (byte)((number % 100)%10);
                            break;
                    }
                    break;

                default:
                    Console.WriteLine("Unknown opcode: {0}", opcode);
                    break;
            }
            pc += 2;
            // Update timers
            if (delay_timer > 0)
                --delay_timer;

            if (sound_timer > 0)
            {
                if (sound_timer == 1)
                    Console.Beep();
                --sound_timer;
            }

            for (int i = 0; i < key.Length; i++)
            {
                key[i] = 0;
            }
        }
    }
}