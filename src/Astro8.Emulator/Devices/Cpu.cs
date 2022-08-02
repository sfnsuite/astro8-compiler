﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using Astro8.Instructions;

namespace Astro8.Devices;

public sealed partial class Cpu<THandler> : IDisposable
    where THandler : Handler
{
    private readonly CpuMemory<THandler> _memory;
    private bool _halt;
    private CpuContext _context;

    public Cpu(CpuMemory<THandler> memory)
    {
        _memory = memory;
    }

    public bool Running => !_halt;

    public int A
    {
        get => _context.A;
        set => _context = _context with { A = value };
    }

    public int B
    {
        get => _context.A;
        set => _context = _context with { B = value };
    }

    public int C
    {
        get => _context.A;
        set => _context = _context with { C = value };
    }

    public int ExpansionPort { get; set; }

    public void Run(int cycleDuration = 0, int instructionsPerCycle = 1)
    {
        if (cycleDuration > 0)
        {
            var sw = Stopwatch.StartNew();
            while (!_halt)
            {
                Step(instructionsPerCycle);

                while (sw.ElapsedTicks < cycleDuration)
                {
                    // Wait
                }

                sw.Restart();
            }
        }
        else
        {
            Step(0);
        }
    }

    public void RunThread(int cycleDuration = 0, int instructionsPerCycle = 1)
    {
        var cpuThread = new Thread(() =>
        {
            Run(cycleDuration, instructionsPerCycle);
        });

        cpuThread.Start();
    }

    public void Halt()
    {
        _halt = true;
    }

    public unsafe void Step(int amount = 1)
    {
        if (_halt)
        {
            return;
        }

        var instructionLength = _memory.Instruction.Length;

        fixed (int* dataPointer = _memory.Data)
        fixed (InstructionReference* instructionPointer = _memory.Instruction)
        {
            var context = new StepContext(
                _memory,
                dataPointer,
                instructionPointer,
                instructionLength
            );

            // Store current values on the stack
            context.Cpu = _context;

            for (var i = 0; (amount == 0 || i < amount) && !_halt; i++)
            {
                context.Cpu.MemoryIndex = context.Cpu.ProgramCounter;

                if (context.Cpu.MemoryIndex >= instructionLength)
                {
                    _halt = true;
                    break;
                }

                context.Cpu.ProgramCounter += 1;
                context.Instruction = *(instructionPointer + context.Cpu.MemoryIndex);

                Step(ref context);
            }

            // Restore values from the stack
            _context = context.Cpu;
        }
    }

    public void Save(Stream stream)
    {
        using var writer = new BinaryWriter(stream);
        writer.Write(A);
        writer.Write(B);
        writer.Write(C);
        writer.Write(ExpansionPort);
        writer.Write(_halt);
        _memory.Save(writer);
        _context.Save(writer);
        writer.Flush();
    }

    public void Load(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        A = reader.ReadInt32();
        B = reader.ReadInt32();
        C = reader.ReadInt32();
        ExpansionPort = reader.ReadInt32();
        _halt = reader.ReadBoolean();
        _memory.Load(reader);
        _context = CpuContext.Load(reader);
    }

    private unsafe ref struct StepContext
    {
        private readonly CpuMemory<THandler> _cpuMemory;
        private readonly int* _memoryPointer;
        private readonly InstructionReference* _instructionPointer;
        private readonly int _instructionLength;

        public StepContext(
            CpuMemory<THandler> cpuMemory,
            int* memoryPointer,
            InstructionReference* instructionPointer,
            int instructionLength)
        {
            _cpuMemory = cpuMemory;
            _memoryPointer = memoryPointer;
            _instructionPointer = instructionPointer;
            _instructionLength = instructionLength;
        }

        public CpuContext Cpu;
        public InstructionReference Instruction;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int id)
        {
            return *(_memoryPointer + id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int address, int value)
        {
            *(_memoryPointer + address) = value;
            _cpuMemory.OnChange(address, value);

            if (address < _instructionLength)
            {
                *(_instructionPointer + address) = new InstructionReference(value);
            }
        }
    }

    public void Dispose()
    {
        Halt();
    }
}
