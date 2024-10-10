# Liquid-v8.0
Liquid Application Framework - v8.0

# Develop and Run Liquid Modern Applications
Liquid is a **multi-cloud** framework designed to **accelerate the development** of cloud-native microservices while avoiding coupling your code to specific cloud providers. 

When writing Liquid applications, you stop worrying about the technology and focus on your business - Liquid abstracts most of the boilerplate and let you just write domain code that looks great and gets the job done.

# Forked from [Liquid v6.0](https://github.com/sergiopaim/liquid-6.0)
This version is an evolution of [Liquid v6.0](https://github.com/sergiopaim/liquid-6.0).

This fork, which jumps directly to version v8.0, upgrades from .NET Core v6.0 to .NET v8.0.

Finally it brings:
- Valuable improvements and additions related to the adherence to advanced DDD concepts (such as policy specifications and rich domain classes)
- Minor syntax breaks to achieve greater code standardization

## Features

- Abstracts a number of services from cloud providers such as Azure and Google Cloud to enable you to write code that will run anywhere.
- Brings a prescribed programming model that will save you time on thinking how to structure your application, allowing you to focus on writing business code.

# Getting Started

To use Liquid, you create a new base .NET Web API Application and install the following nuget packages:

- `LiquidApplication.Platform`

And then choose what implementation cartridge you need to run your environment:

- If you'll deploy your application to Azure, install `LiquidApplication.OnAzure`
- If you'll deploy your application to Google, install `LiquidApplication.OnGoogle`

# Contribute
Some of the best ways to contribute are to try things out, file issues, and make pull-requests.

- You can provide feedback by filing issues on GitHub. We accept issues, ideas and questions. 
- You can contribute by creating pull requests for the issues that are listed. Look for issues marked as _good first issue_ if you are new to the project.

In any case, be sure to take a look at [the contributing guide](CONTRIBUTING.md) before starting.