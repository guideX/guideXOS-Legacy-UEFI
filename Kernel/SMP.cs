using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
namespace guideXOS {
    /// <summary>
    /// SMP
    /// </summary>
    public static unsafe class SMP {
        /// <summary>
        /// Base Address for SMP structures
        /// </summary>
        public const ulong BaseAddress = 0x50000;
        /// <summary>
        /// APMain https://wiki.osdev.org/Memory_Map_(x86)
        /// </summary>
        public const ulong APMain = BaseAddress + 0x0;
        /// <summary>
        /// Stacks for all processors
        /// </summary>
        public const ulong Stacks = BaseAddress + 0x8;
        /// <summary>
        /// Shared GDT for all processors
        /// </summary>
        public const ulong SharedGDT = BaseAddress + 0x16;
        /// <summary>
        /// Shared IDT for all processors
        /// </summary>
        public const ulong SharedIDT = BaseAddress + 0x24;
        /// <summary>
        /// Represents the shared page table for all processors
        /// </summary>
        public const ulong SharedPageTable = BaseAddress + 0x1000;
        /// <summary>
        /// Represents the memory address offset for the trampoline function.
        /// </summary>
        /// <remarks>This constant defines the trampoline's address as an offset of <c>0x10000</c> from
        /// the <see cref="BaseAddress"/>. It is typically used in scenarios where a fixed memory location is required
        /// for function redirection or low-level operations.</remarks>
        public const ulong Trampoline = BaseAddress + 0x10000;
        /// <summary>
        /// Represents the number of active processors on the system.
        /// </summary>
        /// <remarks>This value is initialized to 0 and should be updated to reflect the actual number of
        /// active processors.</remarks>
        public static ulong NumActivedProcessors = 0;
        /// <summary>
        /// Represents the default stack size, in bytes, allocated for each CPU.
        /// </summary>
        /// <remarks>This constant is used to define the memory allocation for stack space per CPU. The
        /// value is set to 1,048,576 bytes (1 MB).</remarks>
        private const int StackSizeForEachCPU = 1048576;
        /// <summary>
        /// Gets the number of CPUs available on the system.
        /// </summary>
        public static int NumCPU { get => ACPI.LocalAPIC_CPUIDs.Count; }

        public static uint ThisCPU => LocalAPIC.GetId();


        //Method for other CPU cores
        //GDT, IDT, PageTable has been configured in Trampoline. so we don't need to set it here
        public static void ApplicationProcessorMain(int Core) {
            *(ulong*)Stacks += StackSizeForEachCPU;
            SSE.enable_sse();
            LocalAPIC.Initialize();
            LocalAPICTimer.StartTimer(1000, 0x20);
            ThreadPool.Initialize();
            NumActivedProcessors++;
            for (; ; ) Native.Hlt();
        }

        public static void Initialize(uint trampoline) {
            if (ThisCPU != 0) Panic.Error("Error: Bootstrap CPU is not CPU 0");

            NumActivedProcessors = 1;

            ulong* apMain = (ulong*)APMain;
            *apMain = (ulong)(delegate*<int, void>)&ApplicationProcessorMain;

            ulong* stacks = (ulong*)Stacks;
            *stacks = (ulong)Allocator.Allocate((ulong)(ACPI.LocalAPIC_CPUIDs.Count * StackSizeForEachCPU));
            *stacks += StackSizeForEachCPU;

            fixed (GDT.GDTDescriptor* gdt = &GDT.gdtr) {
                ulong* sgdt = (ulong*)SharedGDT;
                *sgdt = (ulong)gdt;
            }

            fixed (IDT.IDTDescriptor* idt = &IDT.idtr) {
                ulong* sidt = (ulong*)SharedIDT;
                *sidt = (ulong)idt;
            }

            Console.WriteLine("[SMP] Starting all CPUs");
            for (int i = 0; i < NumCPU; ++i) {
                uint id = ACPI.LocalAPIC_CPUIDs[i];
                if (id != ThisCPU) {
                    ulong last = NumActivedProcessors;
                    LocalAPIC.SendInit(id);
                    LocalAPIC.SendStartup(id, (trampoline >> 12));
                    while (last == NumActivedProcessors) Native.Nop();
                }
            }
            Console.WriteLine($"[SMP] {NumCPU} CPUs started");
        }
    }
}