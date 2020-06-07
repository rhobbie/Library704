# Library704
Verilog simulation of an IBM 704

The goal of this project is to have cycle and logic exact simulaton of an IBM 704 based on the existing schematics
from the bitsavers IBM704 directory. See http://www.bitsavers.org/pdf/ibm/704/  (Files 704_schemVol1.pdf 704_schemVol2.pdf  704_schemVol4.pdf)

The diagrams are stored here as textfiles in a custom netlist-like-format and then converted into Verilog by the C# program.
(for a syntax description see the comments of file "Program.cs")

Current state:
  - The IBM 704 CPU with all instructions, core ,emory and operators panel lights/buttons/swichtes are working.
  - Cardreader, printer and punch similations exsits which reads/writes the data via Verilog file I/O.
  - No Tape and Drum simulation.
  - Some diagrams are missing in the existing schematics. These were rebuild from the context and from the other existing IBM 704 documents.
  - The generated verilog file runs only in Icarus Verilog. 
  - The simulaton is very slow.
  
Instructions:
1. Install Icarus Verilog from here http://bleyer.org/icarus/
   Perform full installation (includes GTKWave)
   Select "Add executable folder(s) to the user PATH" during install.


2. Clone or download this project into Visual Studio 2017 or 2019
   
3. Compile and run the project.
   It will read all *.txt module files and converts them into verilog 
   and writes them into a single file I704.v located in subfolder "Testbench"
   
4. Open windows explorer and browse into subfolder Testbench.

5. Double click on Sort.cmd

   This will run Icarus verilog with the generated I704.v and the testbench file system_tb_sort.v.
   
   The testbench writes a short bubble-sort program into the memory and then pressed the start button.
   (from "Coding_for_the_MIT-IBM_704_Computer_Oct57.pdf" page 54)
   
   The program runs for a few seconds. Then GTK Wave is openend with the simulation result.
   
   In gtkwave you see then main machine cycles states and the registers as shown at the operators panel.
   
     I-Time= instruction fetch/decoding.  
     E-Time= instruction execution with core memory access.     
     ER-Time= instruction execution without core memory access     
      
   The entries A1-A10 are the memory cells that are sorted.
      
 6. Close Gtkwave and double click on LOAD_CR.cmd.
    This runs the testbench system_tb_LOAD_CR.v.
    It presses the "Load Card" button of the machine.
    Then the simulated IBM 704 boots from the cardreader the file "CRD.cbn"
    (This is a small HELLO WORLD program generated with the UASAP assembler and the UASPH1 routine from the share tape)
    A trace is printed in the console window.
    After several minutes you will have the printer output in the file LPT.txt.
    
    Its possible to run other carddecks by renaming it to CRD.cbn and putting it in this directory.
    This will work as long as no tape or drum is used.
