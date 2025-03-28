
# 🚀 Advanced .NET Features Showcase  

## Overview  

This repository is a **central hub** for all the advanced .NET and ASP.NET Core features I introduce on **LinkedIn**. It provides hands-on implementations of key concepts with real-world examples, allowing you to explore, test, and integrate them into your projects.  

### 🔹 What's Inside?  
- ✅ **Feature Management & Feature Flags**
- ✅ **IdempotentFusion (Idempotent Api with optional lock)**
- ✅ **Manual Mediator with Pipeline Behavior**  
- ✅ **Api Versioning Strategies**  
- ✅ **Generic and Reusable Api Validations**  
- ✅ **Middleware Dynamic Caching**
- ✅ **App Initializer**  
- ✅ **High-Performance Caching (Memcached, Redis, etc.)**  
- ✅ **Distributed Rate Limiting with YARP & Memcached**  
- ✅ **Advanced API Design & Middleware**  
- ✅ **Optimized Data Processing Techniques**  
- ✅ **Performance Improvements & Best Practices**  
- 🧩 **Design Patterns**  

Each feature is structured for **easy exploration** and **practical implementation**.  

---

## ✨ Featured Example: Distributed Rate Limiting with YARP & Memcached  

This example demonstrates **distributed rate limiting** using **YARP (Yet Another Reverse Proxy)** and **Memcached**. This approach ensures that API rate limits are enforced consistently across distributed instances.  

### 🛠 Setup  

#### 1️⃣ Start Memcached  
Run the following command to spin up Memcached:  

```
docker-compose up -d
```  

#### 2️⃣ Configure YARP Rate Limiting  
The **YARP reverse proxy** is configured to limit incoming requests based on **IP-based quotas stored in Memcached**.  

### 📌 Example Rate Limit Policy  
- **100 requests per minute per IP**  
- Requests exceeding the limit receive a `429 Too Many Requests` response  

#### 3️⃣ Test the Rate Limiter  
Use a tool like **Postman** or **cURL** to send multiple requests:  

```
curl -X GET http://localhost:5000/api/resource -H "Authorization: Bearer <your_token>"
```  

Once the limit is reached, you’ll receive:  

```json
{
  "error": "Too Many Requests"
}
```  

---

## ✨ Featured Example: Advanced Feature Management with Feature Filters  

This example demonstrates **feature management in ASP.NET Core** using feature filters. Feature filters allow conditional feature toggling based on factors like user claims, enabling personalized experiences.  

### 🛠 Setup  

#### 1️⃣ Start Memcached  
To enable the **Memcached-based feature toggle**, run:  

```
docker-compose up -d
```  

#### 2️⃣ Generate a JWT Token  
To test VIP-based feature toggling, generate a JWT token using the following credentials:  
- **Username**: `vipuser`  
- **Password**: `vippassword`  

#### 3️⃣ Send a Request  
Once you have the JWT token, call the API:  

```
GET /custom-greeting
Authorization: Bearer <your_jwt_token_here>
```  

---

## 🧩 Design Patterns  

This section contains **various design patterns** implemented in .NET, showcasing real-world use cases and best practices. Some of the patterns you'll find include:

- **Mediator Pattern**
- **Adapter Pattern**
- **Decorator Pattern** 
- **Singleton Pattern**  
- **Factory Pattern**  
- **Repository Pattern**  
- **Strategy Pattern**  
- **CoR Pattern**  
- **Observer Pattern**  
- **Dependency Injection**  

Each pattern is demonstrated with clear code examples and explanations of when and why to use them.  

---

## 📌 Stay Updated  
I regularly share **new features** and **deep-dive explanations** on **[LinkedIn](https://www.linkedin.com/in/mhhoseini)**. Follow along to stay up to date!  

---

🔹 **Contributions**: PRs and discussions are welcome!  

