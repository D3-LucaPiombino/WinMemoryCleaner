# Windows Memory Cleaner (Headless windows service)

> This is a fork of the code from [Igor Mundstein](https://github.com/IgorMundstein/WinMemoryCleaner).
> If you prefer to use an interactive GUI, you should use the original application instead.


[![](https://img.shields.io/badge/Windows-Vista%20|%207%20|%208%20|%2010%20|%2011-blue?style=for-the-badge)](#)
[![](https://img.shields.io/badge/Windows%20Server-2008%20|%202012%20|%202016%20|%202019%20|%202022-blue?style=for-the-badge)](#)

[![](https://img.shields.io/github/license/D3-LucaPiombino/WinMemoryCleaner?style=for-the-badge)](#) 
[![](https://img.shields.io/github/downloads/D3-LucaPiombino/WinMemoryCleaner/total?style=for-the-badge)](https://github.com/D3-LucaPiombino/WinMemoryCleaner/releases) 

The app is a free RAM cleaner. There are times when programs do not release the memory they used, making the machine slow, but you 
don’t want to restart the system to get the used memory back. That is where you use Windows Memory Cleaner to clean your memory, 
so you can carry on working without wasting time restarting your Windows. 

## 🚀 How it works

It's portable, so you do not have to bother with installation or configuration. 
Download and extract the archive in the from which you want to run the service. 
The service requires **administrator** privileges to run and comes with a minimalistic interface; 


It gives you the ability to clean up the memory in 6 different ways:

- `Clean Combined Page List` - Flushes blocks from the combined page list.
- `Clean Modified Page List` - Flushes memory from the Modified page list, writing unsaved data to disk and moving the pages to the Standby list.
- `Clean Processes Working Set` - Removes memory from all user-mode and system working sets and moves it to the Standby or Modified page lists. 
   Note that by the time, processes that run any code will necessarily populate their working sets to do so.
- `Clean Standby List`* - Discards pages from all Standby lists, and moves them to the Free list.
- `Clean Standby List (Low Priority)` - Flushes pages from the lowest-priority Standby list to the Free list.
- `Clean System Working Set` - Removes memory from the system cache working set.

> **NOTE**: at this time there is no way to configure the default options for the service.
> It will always run with 
>
> - `Clean Combined Page List`: true
> - `Clean Modified Page List`: true
> - `Clean Processes Working Set`: true
> - `Clean Standby List`: true
> - `Clean Standby List (Low Priority)`: false
> - `Clean System Working Set`: true

## 📖 Logs
Logs are saved on windows event.

![](/docs/windows-event-log.png)

## ❤️ Donate
This is mostly a modification of the work done by [Igor Mundstein](https://github.com/IgorMundstein/WinMemoryCleaner).
Please, If you have found the original app or this fork helpful and want to support someone, you should use the link at 
the end of [his repo](https://github.com/IgorMundstein/WinMemoryCleaner).
