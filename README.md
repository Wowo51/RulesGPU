# RulesGPU
A rules engine with GPU support.</br>
</br>
RulesGPU is a DMN 1.3 decision-table rules engine that compiles every rule condition into TorchSharp tensors and evaluates them in parallel on the fastest available device (CUDA GPU or CPU). It is stateless, deterministic, and optimized for bulk “record-against-table” classification, supporting the FIRST, UNIQUE, and COLLECT hit policies across numeric, string, boolean, and date types.</br>
</br>
Pure C#, no binaries. No dependencies except for Microsoft's unit testing and TorchSharp.
</br>
[GUI Guide, use the GUI to run the rules engine.](GUIGuide.md)</br>
[Developer Guide, use the rules engine in your own app.](DeveloperGuide.md)</br>
[Code Description, analyze how the rules engine works.](CodeDescription.md)</br>
</br>
RulesGPU is free for non-commercial use, free to test. You need a commercial license to use it commercially. A commercial license is $100 Canadian.</br>
[Non-commercial license.](License.txt)</br>
[Commercial license, purchasing info.](https://transcendai.tech/paylanding.html)</br>
</br>
RulesGPU was built with Raven. Raven is an autonomous AI ReAct agent with first class C# code generation support.</br>
[Raven, 10x coder = 1/10 cost.](https://transcendai.tech)</br>
![AI Image](RavenTextA.jpg)
</br>
Copyright [TranscendAI.tech](https://TranscendAI.tech) 2025.<br>
Authored by Warren Harding. AI assisted.</br>
